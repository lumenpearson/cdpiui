using CDPIUI.Core.System;
using CDPIUI.Helper.LScript;
using CDPIUI.Shared;
using Microsoft.UI.Xaml;

namespace CDPIUI.Views.CreateConfigHelper;

// The buttons that open the site list and binary folders of the edited config.
public sealed partial class CreateNewConfigPage
{
    private void CheckFolderOpenButtonsVisibility()
    {
        OpenBinsConfigDirectory.Visibility = Visibility.Collapsed;
        OpenListsConfigDirectory.Visibility = Visibility.Collapsed;
        if (!string.IsNullOrEmpty(_listDirectory) && _configItem != null)
        {
            OpenListsConfigDirectory.Visibility = Visibility.Visible;
        }
        if (!string.IsNullOrEmpty(_binDirectory) && _configItem != null)
        {
            OpenBinsConfigDirectory.Visibility = Visibility.Visible;
        }
    }

    private void OpenListsConfigDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_listDirectory) && _configItem != null)
        {
            ShellHelper.LookupDirectory(LScriptLangHelper.ExecuteScript(_listDirectory, callItemId: SharedConstants.LocalUserItemsId));
        }
    }

    private void OpenBinsConfigDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_binDirectory) && _configItem != null)
        {
            ShellHelper.LookupDirectory(LScriptLangHelper.ExecuteScript(_binDirectory, callItemId: SharedConstants.LocalUserItemsId));
        }
    }
}
