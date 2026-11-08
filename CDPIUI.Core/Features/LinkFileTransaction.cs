using System.Diagnostics;

namespace CDPIUI.Core.Features;

/// <summary>Publishes a prepared link and restores the displaced file if publication fails.</summary>
internal static class LinkFileTransaction
{
    internal static void Restore(string destination, string? backup)
    {
        if (backup == null)
        {
            File.Delete(destination);
            return;
        }
        // A missing backup must not remove the link that still works.
        _ = File.GetAttributes(backup);
        string? displaced = null;
        bool restored = false;
        try
        {
            if (GetAttributes(destination).HasValue)
            {
                displaced = $"{destination}.{Guid.NewGuid():N}.previous";
                File.Move(destination, displaced);
            }
            try
            {
                File.Move(backup, destination);
                restored = true;
            }
            catch (Exception restoreError)
            {
                if (displaced != null)
                {
                    try { File.Move(displaced, destination); }
                    catch (Exception rollbackError)
                    {
                        throw new IOException($"Cannot restore {destination}. The link is kept at {displaced}; backup at {backup}.",
                            new AggregateException(restoreError, rollbackError));
                    }
                }
                throw;
            }
        }
        finally
        {
            if (restored && displaced != null)
                DeleteTemporary(displaced);
        }
    }

    internal static string? Replace(string destination, bool replacingLink, Action<string> createLink)
    {
        string temporary = $"{destination}.{Guid.NewGuid():N}.link";
        string? displaced = null;
        bool published = false;
        try
        {
            createLink(temporary);
            FileAttributes? attributes = GetAttributes(destination);
            if (attributes.HasValue)
            {
                replacingLink |= (attributes.Value & FileAttributes.ReparsePoint) != 0;
                displaced = replacingLink ? $"{destination}.{Guid.NewGuid():N}.previous" : destination + ".bak";
                // An existing backup is recovery data, never an expendable scratch file.
                File.Move(destination, displaced);
            }

            try
            {
                File.Move(temporary, destination);
                published = true;
            }
            catch (Exception publishError)
            {
                if (displaced != null)
                {
                    try
                    {
                        File.Move(displaced, destination);
                    }
                    catch (Exception restoreError)
                    {
                        throw new IOException($"Cannot restore {destination}. The displaced file is kept at {displaced}.",
                            new AggregateException(publishError, restoreError));
                    }
                }
                throw;
            }

            return replacingLink ? null : displaced;
        }
        finally
        {
            DeleteTemporary(temporary);
            if (published && replacingLink && displaced != null)
                DeleteTemporary(displaced);
        }
    }

    private static FileAttributes? GetAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static void DeleteTemporary(string path)
    {
        try { File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"[LinkFileTransaction] Cannot delete temporary file {path}: {exception.Message}");
        }
    }
}
