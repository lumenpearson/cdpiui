using System.IO;
using CDPIUI.Core;
using CDPIUI.Core.System;

namespace CDPIUI.Helper.CreateConfigHelper;

// The saved choice of the application that opens text files (site lists).
internal static class TextFileOpenPreferences
{
    private const string Section = "FILEOPENACTIONS";

    public static bool ShouldAskForApplication()
    {
        int openMode = SettingsManager.Instance.GetValue<int>(
            Section,
            "mode");
        string selectedApplication = SettingsManager.Instance.GetValue<string>(
            Section,
            "applicationPath");
        bool choiceWasSaved = SettingsManager.Instance.GetValue<bool>(
                Section,
                "isDialogShown") &&
            (openMode == (int)TextFileOpenModes.FollowSystem ||
             (openMode == (int)TextFileOpenModes.UserChoose && File.Exists(selectedApplication)));
        bool askAgain = !SettingsManager.Instance.GetValueOrDefault<bool>(
            Section,
            "doNotRemindAgain",
            defaultValue: true);
        return !choiceWasSaved || askAgain;
    }

    public static void RememberDialogShown() =>
        SettingsManager.Instance.SetValue(Section, "isDialogShown", true);
}
