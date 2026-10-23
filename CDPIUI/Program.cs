using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CDPIUI.Shared.Migration;
using Windows.ApplicationModel.Activation;

namespace CDPIUI
{
    // https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance
    public class Program
    {
        private static SynchronizationContext uiContext;
        private static readonly object activationSyncRoot = new();
        private static readonly Queue<AppActivationArguments> pendingActivations = new();
        private static int migrationActivationPending;

        internal static AppActivationArguments InitialActivationArguments { get; private set; }
        internal static bool IsMigrationActivationPending =>
            Volatile.Read(ref migrationActivationPending) != 0;

        internal static void MarkMigrationActivationPending() =>
            Interlocked.Exchange(ref migrationActivationPending, 1);

        internal static void CompleteMigrationActivation() =>
            Interlocked.Exchange(ref migrationActivationPending, 0);

        [STAThread]
        static int Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            ObserveMigrationArguments(args);
            bool isRedirect = DecideRedirection();

            if (!isRedirect)
            {
                Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(
                        DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    lock (activationSyncRoot)
                    {
                        uiContext = context;
                    }
                    _ = new App();
                    DispatchPendingActivations();
                });
            }

            return 0;
        }

        private static bool DecideRedirection()
        {
            bool isRedirect = false;
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            ExtendedActivationKind kind = args.Kind;
            ObserveMigrationActivation(args);
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey("CDPI_GUIApp");

            if (keyInstance.IsCurrent)
            {
                InitialActivationArguments = args;
                keyInstance.Activated += OnActivated;
            }
            else
            {
                isRedirect = RedirectActivationTo(args, keyInstance);
                if (!isRedirect)
                {
                    // The main instance did not take the activation, usually because it is
                    // exiting: try to become the main instance instead of leaving silently.
                    keyInstance = AppInstance.FindOrRegisterForKey("CDPI_GUIApp");
                    if (keyInstance.IsCurrent)
                    {
                        InitialActivationArguments = args;
                        keyInstance.Activated += OnActivated;
                    }
                    else
                    {
                        isRedirect = true;
                    }
                }
            }

            return isRedirect;
        }

        private static void OnActivated(object sender, AppActivationArguments args)
        {
            ObserveMigrationActivation(args);
            DispatchOrQueueActivation(args);
        }

        private static void DispatchOrQueueActivation(AppActivationArguments args)
        {
            SynchronizationContext context;
            lock (activationSyncRoot)
            {
                context = uiContext;
                if (context == null)
                {
                    pendingActivations.Enqueue(args);
                    return;
                }
            }

            context.Post(_ => DispatchActivation(args), null);
        }

        private static void DispatchPendingActivations()
        {
            AppActivationArguments[] activations;
            lock (activationSyncRoot)
            {
                activations = pendingActivations.ToArray();
                pendingActivations.Clear();
            }

            foreach (AppActivationArguments activation in activations)
            {
                DispatchOrQueueActivation(activation);
            }
        }

        private static void DispatchActivation(AppActivationArguments args)
        {
            try
            {
                if (Application.Current is App app)
                {
                    // PreferRequestedActions catches and logs its own errors.
                    _ = app.PreferRequestedActions(args);
                }
            }
            catch (Exception exception)
            {
                CDPIUI.Core.Basic.Logger.Instance.CreateWarningLog(nameof(Program), exception.ToString());
            }
        }

        private static void ObserveMigrationActivation(AppActivationArguments args)
        {
            if (args.Kind is ExtendedActivationKind.Launch &&
                args.Data is ILaunchActivatedEventArgs launchArguments)
            {
                ObserveMigrationArguments(launchArguments.Arguments);
            }
        }

        private static void ObserveMigrationArguments(IEnumerable<string> arguments)
        {
            if (GoodbyeDpiMigrationActivation.TryFindArgument(arguments, out _))
            {
                MarkMigrationActivationPending();
            }
        }

        private static void ObserveMigrationArguments(string arguments)
        {
            if (GoodbyeDpiMigrationActivation.TryFindArgument(arguments, out _))
            {
                MarkMigrationActivationPending();
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEvent(
            IntPtr lpEventAttributes, bool bManualReset,
            bool bInitialState, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetEvent(IntPtr hEvent);

        [DllImport("ole32.dll")]
        private static extern uint CoWaitForMultipleObjects(
            uint dwFlags, uint dwMilliseconds, ulong nHandles,
            IntPtr[] pHandles, out uint dwIndex);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(uint dwProcessId);

        private static IntPtr redirectEventHandle = IntPtr.Zero;

        // Do the redirection on another thread, and use a non-blocking
        // wait method to wait for the redirection to complete.
        // Returns false when the main instance did not accept the activation in time.
        public static bool RedirectActivationTo(AppActivationArguments args,
                                                AppInstance keyInstance)
        {
            redirectEventHandle = CreateEvent(IntPtr.Zero, true, false, null);
            AllowSetForegroundWindow(keyInstance.ProcessId);
            bool redirected = false;
            // Main runs before the UI thread and its dispatcher exist, so this method must block
            // until the redirection ends. The redirection runs on the thread pool, where no
            // SynchronizationContext is captured, so the await cannot deadlock against the wait below.
            Task.Run(async () =>
            {
                try
                {
                    await keyInstance.RedirectActivationToAsync(args).AsTask().ConfigureAwait(false);
                    redirected = true;
                }
                catch (Exception)
                {
                    // The main instance exited or crashed meanwhile.
                }
                finally
                {
                    // Without the event the wait below never ends and the process stays without a window.
                    SetEvent(redirectEventHandle);
                }
            });

            uint CWMO_DEFAULT = 0;
            uint RedirectTimeoutMs = 30000;
            _ = CoWaitForMultipleObjects(
               CWMO_DEFAULT, RedirectTimeoutMs, 1,
               [redirectEventHandle], out uint handleIndex);

            return Volatile.Read(ref redirected);
        }
    }
}
