using CDPIUI.Core;
using CDPIUI.Core.Basic;
using CDPIUI.Core.ComponentServices.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;
using CDPIUI.Core.ComponentServices.Helpers.Configuration.Helpers;

using CDPIUI.Shared;
using CDPIUI.Shared.Basic.Filesystem;
using CDPIUI.ViewModels;
using CDPIUI.Views.CreateConfigHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Unidecode.NET;
using Windows.ApplicationModel.Chat;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using WinRT.Interop;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.Dialogs.CreateConfigHelper
{
    public enum CreateConfigResult
    {
        Selected,
        Canceled,
        Nothing
    }
    public class FileSelectModel
    {
        public string ConvertDirectoryPath { get; set; }
        public string FilePath { get; set; }
        public string AutoCorrectFilePath { get; set; }
    }
    public sealed partial class SelectUsedFilesForConfigContentDialog : ContentDialog
    {
        public ICommand ChangeFilePathCommand { get; }
        
        public Dictionary<string, string> Files = [];

        private List<string> _files;
        private string ConfigName;
        private string ConfigPath;
        private AskAutoFillMode _askAutoFillMode;
        private ConfigItem ConfigItem;

        public CreateConfigResult Result { get; private set; } = CreateConfigResult.Nothing;

        private ObservableCollection<FileSelectModel> Models = [];

        public SelectUsedFilesForConfigContentDialog(List<string> files, string configName, string configPath, ConfigItem configItem, AskAutoFillMode askAutoFillMode)
        {
            InitializeComponent();
            this.DataContext = this;
            this.Loaded += SelectUsedFilesForConfigContentDialog_Loaded;

            _files = files;
            _askAutoFillMode = askAutoFillMode;
            ConfigName = configName;
            ConfigPath = configPath;
            ConfigItem = configItem;

            ChangeFilePathCommand = new RelayCommand(p => ChangeFilePath((Tuple<string, string>)p));

            CreateModels();
            FilesListView.ItemsSource = Models;

            
        }

        private void SelectUsedFilesForConfigContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (_askAutoFillMode == AskAutoFillMode.Quiet)
            {
                Result = CreateConfigResult.Selected;
                AutoCorrectActions();
                this.Hide();
            }
        }

        // ':', '/', '?' and the like in a config name threw or produced nested folders.
        private static string GetSafeFolderName(string name)
        {
            string value = (name ?? string.Empty).Unidecode().Replace(" ", "_");
            char[] invalid = Path.GetInvalidFileNameChars();
            value = new string(value.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray()).Trim('.', '_');
            if (value.Length > 64) value = value[..64];
            return value.Length == 0 ? "config" : value;
        }

        private void CreateModels()
        {
            
            bool flag = true;

            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;
            string folderName = $"{GetSafeFolderName(ConfigName)}_converted_{secondsSinceEpoch}";

            foreach (var file in _files)
            {
                string convDir;
                
                if (Path.GetExtension(file).Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    convDir = Path.Combine(
                        ConfigurationService.GetItemFolderFromPackId(SharedConstants.LocalUserItemsId),
                        SharedConstants.LocalUserItemSiteListsFolder,
                        folderName);
                }
                else
                {
                    convDir = Path.Combine(
                        ConfigurationService.GetItemFolderFromPackId(SharedConstants.LocalUserItemsId),
                        SharedConstants.LocalUserItemBinsFolder,
                        folderName);
                }
                // The folder is created when a file is copied into it, not before the user confirms.
                string autoCorrectPath = FindAutoCorrectPath(file);
                Models.Add(new FileSelectModel()
                {
                    ConvertDirectoryPath = convDir,
                    FilePath = file,
                    AutoCorrectFilePath = autoCorrectPath
                });

                if (string.IsNullOrEmpty(autoCorrectPath))
                {
                    flag = false;
                }
            }

            IsPrimaryButtonEnabled = flag;

            
        }

        private string FindAutoCorrectPath(string filePath)
        {
            return Autocorrector.FindAutoCorrectPath(filePath, ConfigItem, ConfigPath);
        }

        private void ChangeFilePath(Tuple<string, string> tuple)
        {
            var _m = Models.FirstOrDefault(m => m.FilePath == tuple.Item1);
            if (_m != null)
                Models.Remove(_m);

            _files.Remove(tuple.Item1);
            Files.Add(tuple.Item1, tuple.Item2);

            if (Models.Count == 0)
            {
                IsPrimaryButtonEnabled = true;
            }
        }

        private void RootDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Result = CreateConfigResult.Selected;
            if (Models.Count != 0)
            {
                AutoCorrectActions();
            }
        }
        private ILocalizer localizer = Localizer.Get();
        private async Task ShowDialog(string message)
        {
            try
            {
                var dlg = new MessageDialog(message, localizer.GetLocalizedString("AutoCorrectError"));
                InitializeWithWindow.Initialize(dlg, WindowNative.GetWindowHandle(await ((App)Application.Current).SafeCreateNewWindow<CreateConfigHelperWindow>()));
                await dlg.ShowAsync();
            }
            catch (Exception exception)
            {
                Logger.Instance.CreateWarningLog(nameof(SelectUsedFilesForConfigContentDialog), exception.ToString());
            }
        }

        private void AutoCorrectActions()
        {
            foreach (var model in Models)
            {
                try
                {
                    string filepath = Regex.Replace(model.FilePath, @"%(?<name>[A-Za-z0-9_]+)%", "");
                    Logger.Instance.CreateDebugLog(nameof(SelectUsedFilesForConfigContentDialog), $"{filepath}");

                    Debug.WriteLine(model.AutoCorrectFilePath);

                    if (!string.Equals(Path.GetFileNameWithoutExtension(model.AutoCorrectFilePath), "autohostlist", StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(model.ConvertDirectoryPath);
                        File.Copy(model.AutoCorrectFilePath, Path.Combine(model.ConvertDirectoryPath, Path.GetFileName(filepath)), true);
                    }

                    Files.Add(
                        model.FilePath,
                        "$GETCURRENTDIR()/" +
                        Path.Combine(
                            FileSystemService.GetFolderNamesUpTo(
                                model.ConvertDirectoryPath, SharedConstants.LocalUserItemsId), Path.GetFileName(filepath)
                                )
                        );

                    Logger.Instance.CreateDebugLog(nameof(SelectUsedFilesForConfigContentDialog), $"{model.FilePath}");

                }
                catch (Exception ex)
                {
                    // ShowDialog catches and logs its own errors.
                    _ = ShowDialog($"{ErrorsHelper.Convertor.GetPrettyErrorCode("AUTOCORRECT", ex)}:\n" + ex.Message);
                    Result = CreateConfigResult.Canceled;
                    return;
                }
            }
        }
    }
}
