#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Basic;

// File activation selects the first supported route; operations retain all import/window effects.
internal sealed class AppFileActivationHandler
{
    private static readonly string[] SupportedFilePaths =
        [".cdpisignedpack", ".cdpiconfigpack", ".cdpipatch", ".cdpitask", ".cdpiconfig"];

    private readonly IAppFileActivationOperations _operations;

    public AppFileActivationHandler(IAppFileActivationOperations operations) => _operations = operations;

    public async Task<bool> ProcessFiles(string[] files)
    {
        foreach (string file in files)
        {
            var _file = file.Replace("\"", "");
            _operations.LogFile(_file);

            if (_operations.PathExists(_file) && SupportedFilePaths.Contains(
                Path.GetExtension(_file),
                StringComparer.OrdinalIgnoreCase))
            {
                if (_operations.IsConfigShareSupported(_file))
                {
                    await _operations.OpenConfigShareAsync(_file);
                    return true;
                }

                if (Path.GetExtension(_file).Equals(".cdpitask", StringComparison.OrdinalIgnoreCase))
                {
                    await _operations.OpenConditionalTaskAsync(_file);
                    return true;
                }

                if (Path.GetExtension(_file).Equals(".cdpipatch", StringComparison.OrdinalIgnoreCase))
                {
                    await _operations.OpenUpdateFileAsync(_file);
                    return true;
                }

                await _operations.OpenStorePackAsync(_file);
                return true; // Only first file will be processed.
            }
        }
        return false;
    }
}
