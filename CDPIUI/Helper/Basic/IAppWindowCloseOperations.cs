#nullable enable

using System;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Basic;

internal interface IAppWindowCloseOperations<TWindow> where TWindow : class
{
    Func<bool> CaptureExitEnqueue(TWindow window);
    bool IsMigrationWindow(TWindow window);
    void SavePosition(TWindow window);
    void Detach(TWindow window);
    void ReleaseModal(TWindow window);
    void Warn(Exception error);
    void CompleteMigration();
    void ForgetOwner(TWindow window);
    bool IsStoreWorking();
    Task CreatePrepareAsync();
    void ExitIfIdle();
}
