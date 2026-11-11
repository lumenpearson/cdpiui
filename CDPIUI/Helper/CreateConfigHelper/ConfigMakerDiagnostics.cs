#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CDPIUI.ViewModels;

namespace CDPIUI.Helper.CreateConfigHelper;

// Owns diagnostic publication and localized counters; the host projects actual UI rows.
internal sealed class ConfigMakerDiagnostics<TItem> : ObservableObject
{
    private readonly ObservableCollection<TItem> _items;
    private readonly Func<string> _editor;
    private readonly Func<IReadOnlyList<ComponentCommandHelpOption>> _options;
    private readonly IEnumerable<ConfigMakerVariableDefinition> _variables;
    private readonly Func<string, bool> _resource;
    private readonly Func<ComponentCommandDiagnostic, TItem> _createItem;
    private readonly Func<string, string> _text;
    private readonly Action _refresh;
    private string _errorCountText = string.Empty;
    private string _warningCountText = string.Empty;

    public ConfigMakerDiagnostics(ObservableCollection<TItem> items,
        Func<string> editor, Func<IReadOnlyList<ComponentCommandHelpOption>> options,
        IEnumerable<ConfigMakerVariableDefinition> variables, Func<string, bool> resource,
        Func<ComponentCommandDiagnostic, TItem> createItem, Func<string, string> text, Action refresh)
    {
        _items = items;
        _editor = editor;
        _options = options;
        _variables = variables;
        _resource = resource;
        _createItem = createItem;
        _text = text;
        _refresh = refresh;
    }

    public string ErrorCountText
    {
        get => _errorCountText;
        internal set => SetProperty(ref _errorCountText, value);
    }

    public string WarningCountText
    {
        get => _warningCountText;
        internal set => SetProperty(ref _warningCountText, value);
    }

    public void Rebuild()
    {
        string editorText = _editor();
        List<ComponentCommandDiagnostic> results = ComponentCommandValidationService
            .Validate(editorText, _options())
            .ToList();
        ConfigMakerPresetReferenceValidator.AddDiagnostics(editorText, _variables, _resource, results);

        _items.Clear();
        foreach (ComponentCommandDiagnostic diagnostic in results)
        {
            _items.Add(_createItem(diagnostic));
        }

        int errorCount = results.Count(item => item.Severity == ComponentCommandDiagnosticSeverity.Error);
        int warningCount = results.Count - errorCount;
        ErrorCountText = string.Format(_text("ConfigMakerDiagnosticErrorCount"), errorCount);
        WarningCountText = string.Format(_text("ConfigMakerDiagnosticWarningCount"), warningCount);
        _refresh();
    }

    public string GetSeverityText(ComponentCommandDiagnostic diagnostic) => _text(
        diagnostic.Severity == ComponentCommandDiagnosticSeverity.Error
            ? "ConfigMakerDiagnosticErrorSeverity"
            : "ConfigMakerDiagnosticWarningSeverity");

    public string GetDescription(ComponentCommandDiagnostic diagnostic) => diagnostic.Kind switch
    {
        ComponentCommandDiagnosticKind.UnknownFlag => string.Format(
            _text("ConfigMakerUnknownFlagDiagnostic"), diagnostic.Token),
        ComponentCommandDiagnosticKind.MissingRequiredArgument => string.Format(
            _text("ConfigMakerMissingArgumentDiagnostic"), diagnostic.Token),
        ComponentCommandDiagnosticKind.UnresolvedVariable => string.Format(
            _text("ConfigMakerUnresolvedVariableDiagnostic"), diagnostic.Token),
        ComponentCommandDiagnosticKind.UnresolvedResource => string.Format(
            _text("ConfigMakerUnresolvedResourceDiagnostic"), diagnostic.Token),
        _ => _text("ConfigMakerUnterminatedQuoteDiagnostic"),
    };
}
