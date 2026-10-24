using System;
using System.Linq;
using CDPIUI.Controls.CreateConfigHelper;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Helper.CreateConfigHelper;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI.Controls.BlockCheck2;

public sealed partial class BlockCheck2PresetEditorControl : UserControl
{
    private BlockCheck2PresetDraft? _draft;
    private bool _refreshingEditor;
    private bool _updatingDraftFromEditor;

    public BlockCheck2PresetEditorControl()
    {
        InitializeComponent();
        ConfigMaker.ComponentId = HardcodedItemIds.ComponentIds[Components.Zapret2];
        ConfigMaker.SetCommandPanelVisible(false);
        ConfigMaker.CommandTextChanged += ConfigMaker_CommandTextChanged;
        ConfigMaker.PresetFileReplaced += ConfigMaker_PresetFileReplaced;
    }

    public ConfigMakerUserControl Editor => ConfigMaker;

    public void LoadDraft(BlockCheck2PresetDraft value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (_draft != null)
        {
            _draft.PropertyChanged -= Draft_PropertyChanged;
        }
        _draft = value;
        _draft.PropertyChanged += Draft_PropertyChanged;
        RefreshFromDraft();
    }

    public void SetExpertEditing(bool enabled)
    {
        if (_draft == null)
        {
            return;
        }
        if (enabled)
        {
            _draft.BeginExpertEditing();
        }
        else
        {
            _draft.DiscardExpertChanges();
        }
        RefreshFromDraft();
    }

    private void Draft_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_updatingDraftFromEditor &&
            (e.PropertyName is nameof(BlockCheck2PresetDraft.EffectiveArguments) or
                nameof(BlockCheck2PresetDraft.IsExpertEditingEnabled)))
        {
            RefreshEditor();
        }
        if (e.PropertyName is nameof(BlockCheck2PresetDraft.StructuredArguments) or
            nameof(BlockCheck2PresetDraft.Files))
        {
            RebuildTrees();
        }
    }

    private void RefreshFromDraft()
    {
        RefreshEditor();
        RebuildTrees();
    }

    private void RefreshEditor()
    {
        if (_draft == null)
        {
            return;
        }
        _refreshingEditor = true;
        try
        {
            ConfigMaker.CommandText = ComponentCommandLineFormatter.FormatByFlags(
                _draft.EffectiveArguments);
            ConfigMaker.IsEditorReadOnly = !_draft.IsExpertEditingEnabled;
            ConfigMaker.SetCommandPanelVisible(_draft.IsExpertEditingEnabled);
        }
        finally
        {
            _refreshingEditor = false;
        }
    }

    private void RebuildTrees()
    {
        if (_draft == null)
        {
            ConfigMaker.ClearPresetStructure();
            return;
        }
        ConfigMaker.SetPresetStructure(
            _draft.Files.Select(file => new ConfigMakerPresetFileInfo(
                file.Name,
                file.Path,
                file.Folder,
                file.Kind switch
                {
                    BlockCheck2PresetFileKind.Library => ConfigMakerPresetFileKind.Library,
                    BlockCheck2PresetFileKind.Payload => ConfigMakerPresetFileKind.Payload,
                    _ => ConfigMakerPresetFileKind.SiteList,
                })),
            _draft.Groups.Select(group => new ConfigMakerPresetGroupInfo(
                group.Name,
                [group.Route, group.Scope, group.Strategies])));
    }

    private void ConfigMaker_CommandTextChanged(string text)
    {
        if (!_refreshingEditor && _draft?.IsExpertEditingEnabled == true)
        {
            _updatingDraftFromEditor = true;
            try
            {
                _draft.SetExpertArguments(text);
            }
            finally
            {
                _updatingDraftFromEditor = false;
            }
        }
    }

    private void ConfigMaker_PresetFileReplaced(
        object? sender,
        ConfigMakerPresetFileReplacedEventArgs e)
    {
        if (_draft != null)
        {
            _draft.ApplyFileReferenceChange(
                e.CommandText,
                e.OriginalPath,
                e.ReplacementPath);
        }
    }

}
