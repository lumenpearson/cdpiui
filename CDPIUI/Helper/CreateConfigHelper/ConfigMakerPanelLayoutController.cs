#nullable enable

using System;

namespace CDPIUI.Helper.CreateConfigHelper;

// Applies bounds in order: UI assignments may synchronously reenter the layout handlers.
internal sealed class ConfigMakerPanelLayoutController
{
    private readonly IConfigMakerPanelLayoutView _view;
    private bool _layoutInitialized;
    private bool _updatingLayoutBounds;
    private double _savedEditorHeight = 420;

    public ConfigMakerPanelLayoutController(IConfigMakerPanelLayoutView view) => _view = view;
    public bool IsUpdating => _updatingLayoutBounds;
    public void Invalidate() => _layoutInitialized = false;
    public void RememberEditorHeight(double value) => _savedEditorHeight = value;

    public void Update()
    {
        if (_updatingLayoutBounds || _view.RootWidth <= 0 || _view.RootHeight <= 0)
        {
            return;
        }

        _updatingLayoutBounds = true;
        try
        {
            const double editorMinWidth = 360;
            const double commandMinWidth = 340;
            const double editorMinHeight = 180;
            const double bottomMinHeight = 110;

            double filesWidth = 0;
            if (_view.FilesVisible)
            {
                double maximumFilesWidth = Math.Max(
                    150,
                    _view.RootWidth - editorMinWidth -
                    (_view.CommandsVisible ? commandMinWidth + _view.CommandSizerWidth : 0) -
                    _view.FilesSizerWidth);
                _view.FilesMaxWidth = Math.Max(
                    _view.FilesMinWidth,
                    Math.Min(_view.RootWidth * 0.45, maximumFilesWidth));
                _view.FilesWidth = Math.Clamp(
                    _view.FilesWidth,
                    _view.FilesMinWidth,
                    _view.FilesMaxWidth);
                filesWidth = _view.FilesWidth + _view.FilesSizerWidth;
            }

            double availableMainWidth = Math.Max(editorMinWidth, _view.RootWidth - filesWidth);

            if (_view.CommandsVisible)
            {
                double maxEditorWidth = Math.Max(
                    editorMinWidth,
                    availableMainWidth - commandMinWidth - _view.CommandSizerWidth);
                _view.EditorMaxWidth = maxEditorWidth;
                _view.EditorWidth = maxEditorWidth;
            }
            else
            {
                _view.EditorMaxWidth = availableMainWidth;
                _view.EditorWidth = availableMainWidth;
            }

            double availableMainHeight = Math.Max(editorMinHeight, _view.RootHeight);

            if (_view.BottomVisible)
            {
                double maxEditorHeight = Math.Max(
                    editorMinHeight,
                    availableMainHeight - bottomMinHeight - _view.BottomSizerHeight);
                _view.EditorMaxHeight = maxEditorHeight;
                double requestedHeight = _layoutInitialized
                    ? _view.EditorHeight
                    : Math.Min(_savedEditorHeight, maxEditorHeight);
                _view.EditorHeight = Math.Clamp(requestedHeight, editorMinHeight, maxEditorHeight);
            }
            else
            {
                _view.EditorMaxHeight = availableMainHeight;
                _view.EditorHeight = availableMainHeight;
            }

            _layoutInitialized = true;
        }
        finally
        {
            _updatingLayoutBounds = false;
        }
    }
}
