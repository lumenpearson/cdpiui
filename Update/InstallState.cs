namespace Update;

public enum InstallState
{
    None,
    Prepare,
    Unpack,
    Copy,
    Finalize,
    Completed,
    Error
}
