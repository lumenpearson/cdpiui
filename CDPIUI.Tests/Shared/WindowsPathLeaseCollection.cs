namespace CDPIUI.Tests.Shared;

// Acquisition briefly denies write sharing on common ancestors while it pins their
// children. Other filesystem tests must not attempt a rename in that acquisition window.
[CollectionDefinition("Windows path leases", DisableParallelization = true)]
public sealed class WindowsPathLeaseCollection { }
