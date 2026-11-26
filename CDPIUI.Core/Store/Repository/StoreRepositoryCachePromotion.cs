namespace CDPIUI.Core.Store.Repository;

internal static class StoreRepositoryCachePromotion
{
    internal static string? Promote(
        string extractedDirectory, string targetDirectory, Action<string, string>? moveDirectory = null)
    {
        Action<string, string> move = moveDirectory ?? Directory.Move;
        if (!Directory.Exists(targetDirectory))
        {
            move(extractedDirectory, targetDirectory);
            return null;
        }

        string previousDirectory = targetDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + $".previous-{Guid.NewGuid():N}";
        move(targetDirectory, previousDirectory);
        try
        {
            move(extractedDirectory, targetDirectory);
        }
        catch (Exception publicationError)
        {
            try
            {
                // Never remove an unexpected target to make restoration succeed.
                move(previousDirectory, targetDirectory);
            }
            catch (Exception restorationError)
            {
                throw new RepositoryCacheRecoveryException(
                    previousDirectory, targetDirectory, extractedDirectory,
                    publicationError, restorationError);
            }

            throw;
        }

        // The caller may clean this owned backup only after publication succeeded.
        return previousDirectory;
    }
}

internal sealed class RepositoryCacheRecoveryException : IOException
{
    internal string PreviousCachePath { get; }
    internal string TargetCachePath { get; }
    internal string ExtractedCachePath { get; }

    internal RepositoryCacheRecoveryException(
        string previousCachePath, string targetCachePath, string extractedCachePath,
        Exception publicationError, Exception restorationError)
        : base(
            $"The previous Store cache is retained at '{previousCachePath}'. Automatic restoration failed; " +
            $"inspect target '{targetCachePath}' and staging '{extractedCachePath}'.",
            new AggregateException(publicationError, restorationError))
    {
        PreviousCachePath = previousCachePath;
        TargetCachePath = targetCachePath;
        ExtractedCachePath = extractedCachePath;
    }
}
