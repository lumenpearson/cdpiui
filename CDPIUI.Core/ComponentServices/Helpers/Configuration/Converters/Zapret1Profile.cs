namespace CDPIUI.Core.ComponentServices.Helpers.Configuration.Converters;

/// <summary>
/// The options of one Zapret1 profile, the part of the command line between two --new options.
/// </summary>
internal sealed class Zapret1Profile(int index)
{
    public int Index { get; } = index;

    public List<Zapret1CommandOption> Options { get; } = [];
}
