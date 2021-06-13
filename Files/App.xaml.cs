using Files.CommandLine;
using Files.Common;
using Files.Controllers;
using Files.Filesystem;
using Files.Filesystem.FilesystemHistory;
using Files.Helpers;
using Files.SettingsInterfaces;
using Files.UserControls.MultitaskingControl;
using Files.ViewModels;
using Files.Views;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Toolkit.Uwp;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Files
{
    sealed partial class App : Application
    {
        private static bool ShowErrorNotification = false;

        public static SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);
        public static StorageHistoryWrapper HistoryWrapper = new StorageHistoryWrapper();
        public static IBundlesSettings BundlesSettings = new BundlesSettingsViewModel();
        public static SettingsViewModel AppSettings { get; private set; }
        public static MainViewModel MainViewModel { get; private set; }
        public static JumpListManager JumpList { get; } = new JumpListManager();
        public static SidebarPinnedController SidebarPinnedController { get; private set; }
        public static CloudDrivesManager CloudDrivesManager { get; private set; }
        public static NetworkDrivesManager NetworkDrivesManager { get; private set; }
        public static DrivesManager DrivesManager { get; private set; }
        public static WSLDistroManager WSLDistroManager { get; private set; }
        public static LibraryManager LibraryManager { get; private set; }
        public static ExternalResourcesHelper ExternalResourcesHelper { get; private set; }
        public static OptionalPackageManager OptionalPackageManager { get; private set; } = new OptionalPackageManager();

        public static Logger Logger { get; private set; }
        private static readonly UniversalLogWriter logWriter = new UniversalLogWriter();

        public static StatusCenterViewModel StatusCenterViewModel { get; } = new StatusCenterViewModel();

        public static SecondaryTileHelper SecondaryTileHelper { get; private set; } = new SecondaryTileHelper();

        public static class AppData
        {
            internal static ExtensionManager FilePreviewExtensionManager { get; set; } = new ExtensionManager("com.files.filepreview");
        }

        public App()
        {
            Logger = new Logger(logWriter);

            UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedException;
            InitializeComponent();
            Suspending += OnSuspending;
            LeavingBackground += OnLeavingBackground;

            AppData.FilePreviewExtensionManager.Initialize();
        }

        private static async Task EnsureSettingsAndConfigurationAreBootstrapped()
        {
            if (AppSettings == null)
            {
                AppSettings = await SettingsViewModel.CreateInstance();
            }

            ExternalResourcesHelper ??= new ExternalResourcesHelper();
            await ExternalResourcesHelper.LoadSelectedSkin();

            MainViewModel ??= new MainViewModel();
            SidebarPinnedController ??= await SidebarPinnedController.CreateInstance();
            LibraryManager ??= new LibraryManager();
            DrivesManager ??= new DrivesManager();
            NetworkDrivesManager ??= new NetworkDrivesManager();
            CloudDrivesManager ??= new CloudDrivesManager();
            WSLDistroManager ??= new WSLDistroManager();

            _ = Task.Factory.StartNew(async () =>
            {
                await LibraryManager.EnumerateLibrariesAsync();
                await DrivesManager.EnumerateDrivesAsync();
                await CloudDrivesManager.EnumerateDrivesAsync();
                await NetworkDrivesManager.EnumerateDrivesAsync();
                await WSLDistroManager.EnumerateDrivesAsync();
            });
        }

        private void OnLeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            DrivesManager?.ResumeDeviceWatcher();
        }

        public static Windows.UI.Xaml.UnhandledExceptionEventArgs ExceptionInfo { get; set; }
        public static string ExceptionStackTrace { get; set; }
        public static List<string> pathsToDeleteAfterPaste = new List<string>();

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            await logWriter.InitializeAsync("debug.log");
            SystemInformation.Instance.TrackAppUse(e);

            Logger.Info("App launched");

            bool canEnablePrelaunch = ApiInformation.IsMethodPresent("Windows.ApplicationModel.Core.CoreApplication", "EnablePrelaunch");

            await EnsureSettingsAndConfigurationAreBootstrapped();

            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();
                rootFrame.CacheSize = 1;
                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //...sert a rien cette partie...
                }

                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (canEnablePrelaunch)
                {
                    TryEnablePrelaunch();
                }

                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments, new SuppressNavigationTransitionInfo());
                }
                else
                {
                    if (!(string.IsNullOrEmpty(e.Arguments) && MainPageViewModel.AppInstances.Count > 0))
                    {
                        await MainPageViewModel.AddNewTabByPathAsync(typeof(PaneHolderPage), e.Arguments);
                    }
                }

                Window.Current.Activate();
                Window.Current.CoreWindow.Activated += CoreWindow_Activated;
            }
        }

        private void CoreWindow_Activated(CoreWindow sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == CoreWindowActivationState.CodeActivated ||
                args.WindowActivationState == CoreWindowActivationState.PointerActivated)
            {
                ShowErrorNotification = true;
                ApplicationData.Current.LocalSettings.Values["INSTANCE_ACTIVE"] = Process.GetCurrentProcess().Id;
                if (MainViewModel != null)
                {
                    MainViewModel.Clipboard_ContentChanged(null, null);
                }
            }
        }


        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await logWriter.InitializeAsync("debug.log");

            Logger.Info("App activated");

            await EnsureSettingsAndConfigurationAreBootstrapped();

            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();
                rootFrame.CacheSize = 1;
                Window.Current.Content = rootFrame;
            }

            var currentView = SystemNavigationManager.GetForCurrentView();
            switch (args.Kind)
            {
                case ActivationKind.Protocol:
                    var eventArgs = args as ProtocolActivatedEventArgs;

                    if (eventArgs.Uri.AbsoluteUri == "files-uwp:")
                    {
                        rootFrame.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());
                    }
                    else
                    {
                        var parsedArgs = eventArgs.Uri.Query.TrimStart('?').Split('=');
                        var unescapedValue = Uri.UnescapeDataString(parsedArgs[1]);
                        switch (parsedArgs[0])
                        {
                            case "tab":
                                rootFrame.Navigate(typeof(MainPage), TabItemArguments.Deserialize(unescapedValue), new SuppressNavigationTransitionInfo());
                                break;

                            case "folder":
                                rootFrame.Navigate(typeof(MainPage), unescapedValue, new SuppressNavigationTransitionInfo());
                                break;
                        }
                    }

                    Window.Current.Activate();
                    Window.Current.CoreWindow.Activated += CoreWindow_Activated;
                    return;

                case ActivationKind.CommandLineLaunch:
                    var cmdLineArgs = args as CommandLineActivatedEventArgs;
                    var operation = cmdLineArgs.Operation;
                    var cmdLineString = operation.Arguments;
                    var activationPath = operation.CurrentDirectoryPath;

                    var parsedCommands = CommandLineParser.ParseUntrustedCommands(cmdLineString);

                    if (parsedCommands != null && parsedCommands.Count > 0)
                    {
                        foreach (var command in parsedCommands)
                        {
                            switch (command.Type)
                            {
                                case ParsedCommandType.OpenDirectory:
                                    rootFrame.Navigate(typeof(MainPage), command.Payload, new SuppressNavigationTransitionInfo());

                                    Window.Current.Activate();
                                    Window.Current.CoreWindow.Activated += CoreWindow_Activated;
                                    return;

                                case ParsedCommandType.OpenPath:

                                    try
                                    {
                                        var det = await StorageFolder.GetFolderFromPathAsync(command.Payload);

                                        rootFrame.Navigate(typeof(MainPage), command.Payload, new SuppressNavigationTransitionInfo());

                                        Window.Current.Activate();
                                        Window.Current.CoreWindow.Activated += CoreWindow_Activated;

                                        return;
                                    }
                                    catch (System.IO.FileNotFoundException ex)
                                    {
                                        Debug.WriteLine($"File not found exception App.xaml.cs\\OnActivated with message: {ex.Message}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Exception in App.xaml.cs\\OnActivated with message: {ex.Message}");
                                    }

                                    break;

                                case ParsedCommandType.Unknown:
                                    if (command.Payload.Equals("."))
                                    {
                                        rootFrame.Navigate(typeof(MainPage), activationPath, new SuppressNavigationTransitionInfo());
                                    }
                                    else
                                    {
                                        var target = Path.GetFullPath(Path.Combine(activationPath, command.Payload));
                                        if (!string.IsNullOrEmpty(command.Payload))
                                        {
                                            rootFrame.Navigate(typeof(MainPage), target, new SuppressNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            rootFrame.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());
                                        }
                                    }

                                    Window.Current.Activate();
                                    Window.Current.CoreWindow.Activated += CoreWindow_Activated;

                                    return;
                            }
                        }
                    }
                    break;

                case ActivationKind.ToastNotification:
                    var eventArgsForNotification = args as ToastNotificationActivatedEventArgs;
                    if (eventArgsForNotification.Argument == "report")
                    {
                        SettingsViewModel.ReportIssueOnGitHub();
                    }
                    break;
            }

            rootFrame.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());

            Window.Current.Activate();
            Window.Current.CoreWindow.Activated += CoreWindow_Activated;
        }

        private void TryEnablePrelaunch()
        {
            CoreApplication.EnablePrelaunch(true);
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SaveSessionTabs();

            var deferral = e.SuspendingOperation.GetDeferral();

            LibraryManager?.Dispose();
            DrivesManager?.Dispose();
            deferral.Complete();

#if DEBUG
            Current.Exit();
#endif
        }

        public static void SaveSessionTabs() 
        {
            if (AppSettings != null)
            {
                AppSettings.LastSessionPages = MainPageViewModel.AppInstances.DefaultIfEmpty().Select(tab =>
                {
                    if (tab != null && tab.TabItemArguments != null)
                    {
                        return tab.TabItemArguments.Serialize();
                    }
                    else
                    {
                        var defaultArg = new TabItemArguments() { InitialPageType = typeof(PaneHolderPage), NavigationArg = "NewTab".GetLocalized() };
                        return defaultArg.Serialize();
                    }
                }).ToArray();
            }
        }

        private static void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e) => AppUnhandledException(e.Exception);

        private static void OnUnobservedException(object sender, UnobservedTaskExceptionEventArgs e) => AppUnhandledException(e.Exception);

        private static void AppUnhandledException(Exception ex)
        {
            string formattedException = string.Empty;

            formattedException += "--------- UNHANDLED EXCEPTION ---------";
            if (ex != null)
            {
                formattedException += $"\n>>>> HRESULT: {ex.HResult}\n";
                if (ex.Message != null)
                {
                    formattedException += "\n--- MESSAGE ---";
                    formattedException += ex.Message;
                }
                if (ex.StackTrace != null)
                {
                    formattedException += "\n--- STACKTRACE ---";
                    formattedException += ex.StackTrace;
                }
                if (ex.Source != null)
                {
                    formattedException += "\n--- SOURCE ---";
                    formattedException += ex.Source;
                }
                if (ex.InnerException != null)
                {
                    formattedException += "\n--- INNER ---";
                    formattedException += ex.InnerException;
                }
            }
            else
            {
                formattedException += "\nException is null!\n";
            }

            formattedException += "---------------------------------------";

            Debug.WriteLine(formattedException);

            Debugger.Break(); 

            SaveSessionTabs();
            Logger.Error(ex, formattedException);
            if (ShowErrorNotification)
            {
                var toastContent = new ToastContent()
                {
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "ExceptionNotificationHeader".GetLocalized()
                                },
                                new AdaptiveText()
                                {
                                    Text = "ExceptionNotificationBody".GetLocalized()
                                }
                            },
                            AppLogoOverride = new ToastGenericAppLogo()
                            {
                                Source = "ms-appx:///Assets/error.png"
                            }
                        }
                    },
                    Actions = new ToastActionsCustom()
                    {
                        Buttons =
                        {
                            new ToastButton("ExceptionNotificationReportButton".GetLocalized(), "report")
                            {
                                ActivationType = ToastActivationType.Foreground
                            }
                        }
                    }
                };

                var toastNotif = new ToastNotification(toastContent.GetXml());

                ToastNotificationManager.CreateToastNotifier().Show(toastNotif);
            }
        }

        public static async void CloseApp()
        {
            if (!await ApplicationView.GetForCurrentView().TryConsolidateAsync())
            {
                Application.Current.Exit();
            }
        }
    }
}