using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using CDPIUI.Controls.Dialogs;
using CDPIUI.Core.ComponentServices;
using CDPIUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI;

public sealed partial class GoodCheckReportStrategyUserControl : UserControl
{
    private bool _isPointerOnControl = false;
    private bool _isRunning = false;


    private ILocalizer _localizer = Localizer.Get();

    public static readonly DependencyProperty FlagToggledCommandProperty =
            DependencyProperty.Register(
                nameof(FlagToggledCommand),
                typeof(ICommand),
                typeof(GoodCheckReportStrategyUserControl),
                new PropertyMetadata(null)
            );

    public ICommand FlagToggledCommand
    {
        get => (ICommand)GetValue(FlagToggledCommandProperty);
        set => SetValue(FlagToggledCommandProperty, value);
    }

    public static readonly DependencyProperty FlagToggledCommandParameterProperty =
        DependencyProperty.Register(
            nameof(FlagToggledCommandParameter),
            typeof(object),
            typeof(GoodCheckReportStrategyUserControl),
            new PropertyMetadata(null)
        );

    public object FlagToggledCommandParameter
    {
        get => GetValue(FlagToggledCommandParameterProperty);
        set => SetValue(FlagToggledCommandParameterProperty, value);
    }

    public ICommand ViewFullCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand SetFlagCommand { get; }

    public GoodCheckReportStrategyUserControl()
    {
        InitializeComponent();

        ViewFullCommand = new RelayCommand(p => ViewFullArgs());
        PlayCommand = new RelayCommand(p => Play());
        SetFlagCommand = new RelayCommand(p => SetFlag());
        SetVisible(false);
        // The task manager outlives the report; a control unloaded during a test stayed in memory.
        Unloaded += (_, _) => ComponentTasksManager.Instance.TaskStateUpdated -= ProcessManager_onProcessStateChanged;

        FlagToolTip.Content = _localizer.GetLocalizedString("SetFlag");
        PlayToolTip.Content = _localizer.GetLocalizedString("TestThis");
    }

    public string ComponentId
    {
        get { return (string)GetValue(ComponentIdProperty); }
        set { SetValue(ComponentIdProperty, value); }
    }

    public static readonly DependencyProperty ComponentIdProperty =
        DependencyProperty.Register(
            nameof(ComponentId), typeof(string), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(string.Empty)
        );
    public string Args
    {
        get { return (string)GetValue(ArgsProperty); }
        set { SetValue(ArgsProperty, value); }
    }

    public static readonly DependencyProperty ArgsProperty =
        DependencyProperty.Register(
            nameof(Args), typeof(string), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(string.Empty)
        );
    public string FailureCount
    {
        get { return (string)GetValue(FailureCountProperty); }
        set { SetValue(FailureCountProperty, value); }
    }

    public static readonly DependencyProperty FailureCountProperty =
        DependencyProperty.Register(
            nameof(FailureCount), typeof(string), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(string.Empty)
        );
    public string SuccessCount
    {
        get { return (string)GetValue(SuccessCountProperty); }
        set { SetValue(SuccessCountProperty, value); }
    }

    public static readonly DependencyProperty SuccessCountProperty =
        DependencyProperty.Register(
            nameof(SuccessCount), typeof(string), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(string.Empty)
        );

    public bool Flag
    {
        get { return (bool)GetValue(FlagProperty); }
        set
        {
            SetValue(FlagProperty, value);
            FlagIconButton.Checked = value;
            if (FlagIconButton.Checked)
                FlagIconButton.Visibility = Visibility.Visible;
            FlagToolTip.Content = FlagIconButton.Checked ? _localizer.GetLocalizedString("RemoveFlag") : _localizer.GetLocalizedString("SetFlag");
        }
    }
    public static readonly DependencyProperty FlagProperty =
        DependencyProperty.Register(
            nameof(Flag), typeof(bool), typeof(GoodCheckReportStrategyUserControl), new PropertyMetadata(false)
        );


    private void SetFlag()
    {
        Flag = FlagIconButton.Checked;

        // A cached parameter kept the first state, so the flag could not be removed.
        FlagToggledCommandParameter = Tuple.Create(Args, FlagIconButton.Checked);


        if (FlagToggledCommand != null && FlagToggledCommand.CanExecute(FlagToggledCommandParameter))
        {
            FlagToggledCommand.Execute(FlagToggledCommandParameter);
            return;
        }
    }
    private void Play()
    {
        if (ComponentId != null)
        {
            _ = PlayAsync(); // PlayAsync catches and logs its own errors.
        }
    }

    private async Task PlayAsync()
    {
        try
        {
            if (!_isRunning)
            {
                await ComponentTasksManager.Instance.StopTask(ComponentId);

                ComponentTasksManager.Instance.TaskStateUpdated += ProcessManager_onProcessStateChanged;

                _ = ComponentTasksManager.Instance.CreateAndRunNewTask(ComponentId, Args);

            }
            else
            {
                await ComponentTasksManager.Instance.StopTask(ComponentId);
                PlayIconButton.Checked = false;
                PlayToolTip.Content = _localizer.GetLocalizedString("TestThis");
                if (!_isPointerOnControl)
                    PlayIconButton.Visibility = Visibility.Collapsed;
                ComponentTasksManager.Instance.TaskStateUpdated -= ProcessManager_onProcessStateChanged;
            }
        }
        catch (Exception exception)
        {
            CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(GoodCheckReportStrategyUserControl), exception.ToString());
        }
    }

    private void ProcessManager_onProcessStateChanged(Tuple<string, bool> tuple)
    {
        // Can be raised on a thread pool thread; ComponentId and the button are UI-thread only.
        DispatcherQueue?.TryEnqueue(() => ApplyProcessState(tuple));
    }

    private void ApplyProcessState(Tuple<string, bool> tuple)
    {
        if (tuple.Item1 != ComponentId)
            return;
        if (tuple.Item2)
        {
            PlayIconButton.Checked = true;
            PlayToolTip.Content = _localizer.GetLocalizedString("StopTest");
            PlayIconButton.Visibility = Visibility.Visible;
            _isRunning = true;
        }
        else if (_isRunning)
        {
            PlayIconButton.Checked = false;
            PlayToolTip.Content = _localizer.GetLocalizedString("TestThis");
            if (!_isPointerOnControl)
                PlayIconButton.Visibility = Visibility.Collapsed;
            _isRunning = false;
            ComponentTasksManager.Instance.TaskStateUpdated -= ProcessManager_onProcessStateChanged;
        }
    }

    private void ViewFullArgs()
    {
        ViewApplyArgsContentDialog dialog = new()
        {
            XamlRoot = this.XamlRoot,
            Title = _localizer.GetLocalizedString("ViewArguments"),
            DialogTitle = string.Empty,
            SeparationTextVisible = Visibility.Collapsed,
            Args = [this.Args ?? string.Empty]

        };
        _ = dialog.ShowQueuedAsync();
    }

    private void SetVisible(bool isVisible)
    {
        if (!isVisible)
        {
            if (!PlayIconButton.Checked)
            {
                PlayIconButton.Visibility = Visibility.Collapsed;
            }
            if (!FlagIconButton.Checked)
            {
                FlagIconButton.Visibility = Visibility.Collapsed;
            }
            ViewIconButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            PlayIconButton.Visibility = Visibility.Visible;
            FlagIconButton.Visibility = Visibility.Visible;
            ViewIconButton.Visibility = Visibility.Visible;
        }

    }

    private void UserControl_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOnControl = true;
        SetVisible(true);
    }

    private void UserControl_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOnControl = false;
        SetVisible(false);
    }
}
