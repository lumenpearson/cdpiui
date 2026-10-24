using System.Collections.Generic;

namespace CDPIUI.Controls.CreateConfigHelper;

public sealed record ConfigMakerPresetGroupInfo(
    string Name,
    IReadOnlyList<string> Details);
