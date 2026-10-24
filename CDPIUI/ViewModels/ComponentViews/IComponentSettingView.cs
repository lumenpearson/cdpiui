using System.Collections.Generic;

namespace CDPIUI.ViewModels.ComponentViews;

/// <summary>
/// The dependency properties and notifications of a component setting control that its
/// view model reads and raises.
/// </summary>
public interface IComponentSettingView
{
    string Type { get; }

    string TextValue { get; }

    bool EnableTextInput { get; }

    List<EnumModel> AvailableEnumValues { get; }

    /// <summary>Stores the value in the dependency property without running its logic again.</summary>
    void StoreTextValue(string value);

    void NotifyValueChanged();
}
