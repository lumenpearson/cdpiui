#nullable enable

namespace CDPIUI.Helper.CreateConfigHelper;

internal interface IConfigMakerPanelLayoutView
{
    double RootWidth { get; }
    double RootHeight { get; }
    bool FilesVisible { get; }
    bool CommandsVisible { get; }
    bool BottomVisible { get; }
    double FilesMinWidth { get; }
    double FilesMaxWidth { get; set; }
    double FilesWidth { get; set; }
    double FilesSizerWidth { get; }
    double CommandSizerWidth { get; }
    double BottomSizerHeight { get; }
    double EditorMaxWidth { set; }
    double EditorWidth { set; }
    double EditorMaxHeight { set; }
    double EditorHeight { get; set; }
}
