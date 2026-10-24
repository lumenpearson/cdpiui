using System;
using System.Collections.Generic;
using CDPIUI.Controls.CreateConfigHelper;

namespace CDPIUI.Helper.CreateConfigHelper;

internal sealed class ConfigMakerPresetFileKeyComparer : IEqualityComparer<(ConfigMakerPresetFileKind Kind, string Path)>
{
    public static ConfigMakerPresetFileKeyComparer Instance { get; } = new();

    public bool Equals(
        (ConfigMakerPresetFileKind Kind, string Path) left,
        (ConfigMakerPresetFileKind Kind, string Path) right) =>
        left.Kind == right.Kind &&
        string.Equals(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode((ConfigMakerPresetFileKind Kind, string Path) value) =>
        HashCode.Combine(value.Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(value.Path));
}
