using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CDPIUI.Shared.Models;
using CDPIUI.Shared.Security;
using System.Runtime.InteropServices;

namespace CDPIUI.Shared.Basic.Filesystem;

internal class DirectoryWritePermissionCheckService
{
    public static UnprocessedOperationResultModel<EmptyResult> HasWritePermission(string directory)
    {
        var testFile = Path.Combine(directory, $".write_test_{Guid.NewGuid():N}.tmp");
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var parent = ReadOnlyPathLease.OpenDirectoryForMutation(directory, createIfMissing: false);
                using var file = WritablePathLease.CreateForMove(testFile, parent, null);
                file.DeleteOnClose();
                file.Stream.WriteByte(0);
                file.Stream.Flush(flushToDisk: true);
                return UnprocessedOperationResultModel<EmptyResult>.SuccessResult();
            }
            using (var fs = new FileStream(testFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.WriteByte(0x0);
                fs.Flush();
            }

            File.Delete(testFile);
            return UnprocessedOperationResultModel<EmptyResult>.SuccessResult();
        }
        catch (UnauthorizedAccessException)
        {
            return UnprocessedOperationResultModel<EmptyResult>.UnSuccessResult();
        }
        catch (DirectoryNotFoundException)
        {
            return UnprocessedOperationResultModel<EmptyResult>.UnSuccessResult();
        }
        catch (IOException)
        {
            return UnprocessedOperationResultModel<EmptyResult>.UnSuccessResult();
        }
        catch (Exception ex)
        {
            return UnprocessedOperationResultModel<EmptyResult>.FailureResult(ex);
        }
        finally
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(testFile))
                    File.Delete(testFile);
            }
            catch (Exception ex)
            {
                // The check result is already known; only an empty test file is left.
                System.Diagnostics.Trace.WriteLine($"[DirectoryWritePermissionCheckService] Cannot delete the test file {testFile}: {ex.Message}");
            }
        }
    }
}
