using System.Collections.Generic;

namespace CDPIUI.Helper.CreateConfigHelper;

public sealed class ComponentCommandHelpDocument
{
    public string ComponentId { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public string Usage { get; init; } = string.Empty;
    public string RawText { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public IReadOnlyList<ComponentCommandHelpOption> Options { get; init; } = [];
}
