#nullable enable

using System.Threading.Tasks;

namespace CDPIUI.Helper.Basic;

internal interface IAppFileActivationOperations
{
    void LogFile(string path);
    bool PathExists(string path);
    bool IsConfigShareSupported(string path);
    Task OpenConfigShareAsync(string path);
    Task OpenConditionalTaskAsync(string path);
    Task OpenUpdateFileAsync(string path);
    Task OpenStorePackAsync(string path);
}
