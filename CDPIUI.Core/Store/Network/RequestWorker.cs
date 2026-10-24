using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using CDPIUI.Shared.Secrets;

namespace CDPIUI.Core.Store.Network;

/// <summary>
/// Store-only requests worker
/// </summary>
internal sealed class RequestWorker : IDisposable
{
    private readonly Lazy<HttpClient> _directClient;
    private readonly Lazy<HttpClient> _proxyClient;
    private int _disposed;

    // A client per request leaves a connection pool and sockets behind every time; the workers
    // created for single requests share these instead.
    private static readonly Lazy<HttpClient> SharedDirectClient = new(() => new HttpClient(CreateHandler(proxyEnable: false)));
    private static readonly Lazy<HttpClient> SharedProxyClient = new(() => new HttpClient(CreateHandler(proxyEnable: true)));

    public RequestWorker(HttpClient? client = null)
    {
        _directClient = client != null ? new(() => client) : SharedDirectClient;
        _proxyClient = client != null ? new(() => client) : SharedProxyClient;
    }

    private static HttpClientHandler CreateHandler(bool proxyEnable)
    {
        return new HttpClientHandler { AllowAutoRedirect = !proxyEnable };
    }


    public async Task<HttpResponseMessage> GetAsync(
        string url,
        string? token = null,
        string authenticationScheme = "Bearer",
        bool useStoreUserAgent = false,
        RangeHeaderValue? range = null,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        bool proxyEnable = StoreProxyHelper.UseProxyForStoreConnections;
        using var request = CreateRequest(url, token, authenticationScheme, useStoreUserAgent, proxyEnable);
        request.Headers.Range = range;
        var client = proxyEnable ? _proxyClient.Value : _directClient.Value;
        return await client.SendAsync(request, completionOption, cancellationToken);
    }

    public async Task<string> GetStringAsync(
        string url,
        string? token = null,
        string authenticationScheme = "Bearer",
        bool useStoreUserAgent = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetAsync(url, token, authenticationScheme, useStoreUserAgent,
            completionOption: HttpCompletionOption.ResponseContentRead, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task DownloadFileAsync(
        string url,
        string destinationPath,
        Action<long, long, TimeSpan>? progress = null,
        string? token = null,
        string authenticationScheme = "Bearer",
        bool useStoreUserAgent = false,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetAsync(url, token, authenticationScheme, useStoreUserAgent,
            completionOption: completionOption, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1L;
        EnsureFreeSpace(destinationPath, totalBytes);
        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        var buffer = new byte[81920];
        long totalRead = 0;
        var stopwatch = Stopwatch.StartNew();
        var lastUpdate = stopwatch.Elapsed;

        // HttpClient.Timeout ends with the response headers: a connection that stops sending
        // the body would wait forever, so every read gets its own deadline.
        using var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        while (true)
        {
            stallCts.CancelAfter(ReadStallTimeout);
            int read;
            try
            {
                read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), stallCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The download stopped: no data for {ReadStallTimeout.TotalSeconds} seconds.");
            }
            if (read == 0)
                break;

            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
            totalRead += read;

            var now = stopwatch.Elapsed;
            if ((now - lastUpdate).TotalSeconds >= 1 || totalRead == totalBytes)
            {
                progress?.Invoke(totalRead, totalBytes, now);
                lastUpdate = now;
            }
        }
    }

    private static readonly TimeSpan ReadStallTimeout = TimeSpan.FromSeconds(60);

    private static void EnsureFreeSpace(string destinationPath, long requiredBytes)
    {
        if (requiredBytes <= 0)
            return;
        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(destinationPath));
            if (string.IsNullOrEmpty(root))
                return;
            var drive = new DriveInfo(root);
            if (drive.IsReady && drive.AvailableFreeSpace < requiredBytes)
                throw new IOException($"Not enough free space on {root}: {requiredBytes} bytes are needed, {drive.AvailableFreeSpace} are free.");
        }
        catch (ArgumentException)
        {
            // A network path or another root DriveInfo does not support: no check.
        }
    }

    private static HttpRequestMessage CreateRequest(
        string url, string? token, string authenticationScheme, bool useStoreUserAgent, bool proxyEnable)
    {
        // Downloads carry components and installers without signatures of their own, so plain HTTP
        // would let anyone on the way replace them.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var sourceUri) || sourceUri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("The source URL must be an absolute HTTPS URL.", nameof(url));

        var request = new HttpRequestMessage(HttpMethod.Get, proxyEnable ? CreateProxyUri(url) : sourceUri);
        try
        {
            if (proxyEnable)
                request.Headers.Add("X-Key", Secret.ProxyKey);
            if (useStoreUserAgent)
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CDPIUI_Components_Store", ApplicationInfo.Version));
            // The Store tokens are for the repository hosts only; a catalogue entry may point anywhere.
            if (!string.IsNullOrEmpty(token) && TokenHosts.Contains(sourceUri.Host))
                request.Headers.Authorization = new AuthenticationHeaderValue(authenticationScheme, token);

            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    private static readonly HashSet<string> TokenHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com", "api.github.com", "codeload.github.com", "raw.githubusercontent.com",
        "objects.githubusercontent.com", "gitlab.com",
    };

    private static Uri CreateProxyUri(string url)
    {
        if (string.IsNullOrWhiteSpace(Secret.ProxyKey))
            throw new InvalidOperationException("Secret.ProxyKey is not configured.");
        if (!Uri.TryCreate(Secret.ProxyURL, UriKind.Absolute, out var proxyUri) ||
            proxyUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(proxyUri.UserInfo) ||
            !string.IsNullOrEmpty(proxyUri.Fragment))
            throw new InvalidOperationException("Secret.ProxyURL must be an absolute HTTPS fetch endpoint or server URL.");

        // Escaped: a form-style query parser reads an unescaped "+" of Base64 as a space.
        string encodedUrl = Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(url))
            .TrimEnd('='));
        var builder = new UriBuilder(proxyUri);
        if (builder.Path == "/")
            builder.Path = "/v1/fetch";
        string query = builder.Query.TrimStart('?');
        builder.Query = string.IsNullOrEmpty(query) ? $"u={encodedUrl}" : $"{query}&u={encodedUrl}";
        return builder.Uri;
    }

    // The clients are shared or owned by the caller, so only this worker is marked as disposed.
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
}
