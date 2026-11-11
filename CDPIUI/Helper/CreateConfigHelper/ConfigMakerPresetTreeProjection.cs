using System;
using System.Collections.Generic;
using System.Linq;

namespace CDPIUI.Helper.CreateConfigHelper;

internal static class ConfigMakerPresetTreeProjection
{
    // Collection identity participates in ViewModel property-change notifications.
    public static IReadOnlyList<TResult> Map<TSource, TResult>(
        IEnumerable<TSource> nodes, Func<TSource, TResult> map) => nodes.Select(map).ToList();
}
