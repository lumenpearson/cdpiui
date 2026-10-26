using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;

namespace CDPIUI.Views.CreateConfigHelper;

/// <summary>A strategy the user drags between the lists of the GoodCheck config editor.</summary>
public class DragItem(string args, string siteListName, string directory, bool showGroupModeChooser, List<ChooseGroupModeContentDialog.ByeDPIGroupModes> modes, Guid guid) : INotifyPropertyChanged
{
    public string Args { get; set; } = args;
    public string SiteListName { get; set; } = siteListName;
    public string Directory { get; set; } = directory;
    public Guid Id { get; set; } = guid;

    private bool _showGroupModeChooser = showGroupModeChooser;
    public bool ShowGroupModeChooser
    {
        get => _showGroupModeChooser;
        set => SetField(ref _showGroupModeChooser, value);
    }

    public List<ChooseGroupModeContentDialog.ByeDPIGroupModes> Modes { get; set; } = modes;

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public override string ToString() => Args;
}
