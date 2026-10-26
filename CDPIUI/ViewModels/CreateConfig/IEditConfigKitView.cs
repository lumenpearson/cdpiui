using System.Collections.Specialized;
using System.Threading.Tasks;
using CDPIUI.Controls.Dialogs.CreateConfigHelper;

namespace CDPIUI.ViewModels.CreateConfig;

/// <summary>What <see cref="EditConfigKitViewModel"/> needs from the page: the window status, dialogs and navigation.</summary>
public interface IEditConfigKitView
{
    /// <summary>Shows the status in the window status bar. May be called from any thread.</summary>
    void SetStatus(bool isWorking, string text);

    Task ShowErrorAsync(string message, string title);

    Task ShowFileSaveErrorAsync(string content, string errorDetails);

    /// <summary>Asks how to save the kit as a new kit.</summary>
    Task<SaveConfigKitDialogResultModel> AskSaveAsAsync(string displayName);

    /// <summary>Asks for the config pack file to write. Returns <see langword="null"/> when the user cancelled.</summary>
    string PickConfigPackFile(string fileName);

    void OpenConfigEditor(NameValueCollection parameter);

    /// <summary>Leaves the page after the kit was saved.</summary>
    void LeaveAfterSave();
}
