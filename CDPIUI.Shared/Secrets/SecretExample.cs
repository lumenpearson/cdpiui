using System.Collections.Generic;

namespace CDPIUI.Shared.Secrets
{
    public interface ISecret
    {
        /// <summary>
        /// Token for acces GitHub Store repository.
        /// If value is empty, Store is unavailable.
        /// </summary>
        static string? GitHubToken { get; }
        /// <summary>
        /// Token for acces GitLab Store repository.
        /// If value is empty, Store is unavailable.
        /// </summary>
        static string? GitLabToken { get; }

        /// <summary>
        /// Store reverse proxy HTTPS server URL or full /v1/fetch endpoint.
        /// </summary>
        static string? ProxyURL { get; }
        /// <summary>
        /// Access key sent to the Store reverse proxy in the X-Key header.
        /// The proxy gets no Store token: it fetches with a token of its own.
        /// </summary>
        static string? ProxyKey { get; }

        /// <summary>
        /// Trusted certifications. 
        /// If this value is empty:
        /// SIGNEDPACK files cannot be installed
        /// SIGNEDPATCH files cannot be installed (also application updates may not work)
        /// </summary>
        static List<string>? TrustedCertificates { get; }
    }

    /// <summary>
    /// Example of Secret.cs file.
    /// To build this application, create Secret.cs with a public static class Secret in this
    /// namespace and copy only the members of this example into it (the interface is already
    /// defined here). .github/scripts/create_secret_stub.ps1 creates such a file.
    /// </summary>
    internal class SecretExample : ISecret
    {
        public static string GitHubToken = "";
        public static string GitLabToken = "";

        public const string ProxyURL = "";
        public const string ProxyKey = "";

        public static readonly List<string> TrustedCertificates = [];
    }
}
