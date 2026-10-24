using CDPIUI.Shared.Migration;

namespace CDPIUI.Helper.Migration;

internal sealed class GoodbyeDpiMigrationCoordinator
{
    private readonly object _syncRoot = new();
    private GoodbyeDpiMigrationSession? _currentSession;

    public static GoodbyeDpiMigrationCoordinator Instance { get; } = new();

    public bool TryAccept(
        GoodbyeDpiMigrationActivationRequest request,
        out GoodbyeDpiMigrationSession? session)
    {
        lock (_syncRoot)
        {
            if (_currentSession != null && !_currentSession.IsTerminal)
            {
                if (IsSameSession(_currentSession.Request, request))
                {
                    session = _currentSession;
                    _ = session.ReannounceAsync();
                    return true;
                }

                session = null;
                _ = MigrationStatusPipeClient.SendAsync(
                    request, MigrationSessionState.Failed, 0,
                    "Another migration is already active.", "MIGRATION_BUSY");
                return false;
            }

            if (_currentSession != null && IsSameSession(_currentSession.Request, request))
            {
                session = _currentSession;
                _ = session.ReannounceAsync();
                return true;
            }

            _currentSession?.Dispose();
            _currentSession = new GoodbyeDpiMigrationSession(request);
            session = _currentSession;
            _ = session.UpdateAsync(MigrationSessionState.Accepted, 0);
            session.BeginPreparation();
            return true;
        }
    }

    private static bool IsSameSession(
        GoodbyeDpiMigrationActivationRequest left,
        GoodbyeDpiMigrationActivationRequest right) =>
        left.MigrationId == right.MigrationId &&
        left.ArchiveSha256 == right.ArchiveSha256 &&
        left.ResponsePipeName == right.ResponsePipeName &&
        left.SessionToken == right.SessionToken;
}
