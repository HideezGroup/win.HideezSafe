using HideezClient.Models.Settings;
using HideezClient.Utilities;
using Unity;
using Unity.Lifetime;
using System;
using System.Windows;
using System.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;
using SingleInstanceApp;
using HideezClient.Modules;
using HideezClient.ViewModels;
using Hardcodet.Wpf.TaskbarNotification;
using System.Globalization;
using System.Threading;
using System.IO;
using HideezClient.Modules.ServiceProxy;
using HideezMiddleware.Localize;
using HideezClient.Modules.ServiceWatchdog;
using HideezClient.Modules.DeviceManager;
using HideezClient.Modules.ActionHandler;
using Hideez.ISM;
using WindowsInput;
using HideezClient.PageViewModels;
using HideezClient.Modules.HotkeyManager;
using System.Text;
using HideezMiddleware.Settings;
using Unity.Injection;
using Hideez.SDK.Communication.Log;
using HideezMiddleware;
using HideezClient.Controls;
using System.Reflection;
using HideezClient.Views;
using Microsoft.Win32;
using HideezClient.Messages;
using System.Diagnostics;
using HideezClient.Modules.ButtonManager;
using HideezClient.Utilities.QrCode;
using ZXing;
using HideezClient.Modules.Log;
using Hideez.SDK.Communication.Workstation;
using HideezMiddleware.Workstation;
using HideezClient.Modules.ProximityLockManager;
using Meta.Lib.Modules.PubSub;
using HideezClient.Modules.NotificationsManager;
using Meta.Lib.Modules.PubSub.Messages;
using HideezMiddleware.IPC.Messages;
using HideezClient.Modules.VaultLowBatteryMonitor;
using HideezClient.ViewModels.Controls;
using HideezClient.Modules.BleDeviceUnpairHelper;
using HideezClient.Modules.WorkstationManager;
using HideezMiddleware.ConnectionModeProvider;
using HideezMiddleware.ApplicationModeProvider;
using HideezClient.ViewModels.Dialog;
using HideezClient.Resources;
using HideezClient.Modules.HideDialogsAdapter;
using HideezClient.Messages.Hotkeys;

namespace HideezClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstance
    {
        public static Logger _log = LogManager.GetCurrentClassLogger(nameof(App));
        private IStartupHelper _startupHelper;
        private IWorkstationManager _workstationManager;
        private IWindowsManager _windowsManager;
        private IServiceWatchdog _serviceWatchdog;
        private IDeviceManager _deviceManager;
        private UserActionHandler _userActionHandler;
        private HotkeyModule _hotkeyModule;
        private IButtonManager _buttonManager;
        private MessageWindow _messageWindow;
        private IMetaPubSub _metaMessenger;
        private BleDeviceUnpairHelper _bleDeviceUnpairHelper;

        public static IUnityContainer Container { get; private set; }

        public App()
        {
            App.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            // Fixes System.ComponentModel.Win32Exception (0x80004005): Not enough quota is available to process this command
            // Occurs when we receive 10000 windows messages per second
            BaseCompatibilityPreferences.HandleDispatcherRequestProcessingFailure = BaseCompatibilityPreferences.HandleDispatcherRequestProcessingFailureOptions.Reset;

            SetupExceptionHandling();

            LogManager.EnableLogging();

            _log.WriteLine($"App version: {Assembly.GetEntryAssembly().GetName().Version}");
            _log.WriteLine($"Version: {Environment.Version}");
            _log.WriteLine($"OS: {Environment.OSVersion}");
            _log.WriteLine($"Command: {Environment.CommandLine}");
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {

        }

        void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");

            DispatcherUnhandledException += (s, e) =>
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");

            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        void LogUnhandledException(Exception e, string source)
        {
            try
            {
                LogManager.EnableLogging();

                var fatalLogger = _log ?? LogManager.GetCurrentClassLogger(nameof(App));
                var assemblyName = Assembly.GetExecutingAssembly().GetName();

                fatalLogger.WriteLine($"Unhandled exception in {assemblyName.Name} v{assemblyName.Version}", LogErrorSeverity.Fatal);
                fatalLogger.WriteLine(e, LogErrorSeverity.Fatal);
                LogManager.Flush();
            }
            catch (Exception)
            {
                try
                {
                    Environment.FailFast("An error occured while handling fatal error", e as Exception);
                }
                catch (Exception exc)
                {
                    Environment.FailFast("An error occured while handling an error during fatal error handling", exc);
                }
            }

            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Cleanup();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            InitializeDIContainer();

            var _connectionModeProvider = Container.Resolve<IConnectionModeProvider>();
            _log.WriteLine($"Connection mode: {_connectionModeProvider.ConnectionMode}");

            // Get application mode as soon as possible
            var _applicationModeProvider = Container.Resolve<IApplicationModeProvider>();
            var mode = _applicationModeProvider.GetApplicationMode();
            _log.WriteLine($"Application mode: {mode}");

            if(mode == ApplicationMode.Standalone)
                ResourceManagersProvider.SetResources(typeof(Strings), typeof(Resources.StandaloneStrings.Strings));
            else
                ResourceManagersProvider.SetResources(typeof(Strings));
            // Init settings
            ISettingsManager<HotkeySettings> hotkeySettingsManager = Container.Resolve<ISettingsManager<HotkeySettings>>();
            ApplicationSettings settings = null;
            ISettingsManager<ApplicationSettings> appSettingsManager = Container.Resolve<ISettingsManager<ApplicationSettings>>();

            try
            {
                var appSettingsDirectory = Path.GetDirectoryName(appSettingsManager.SettingsFilePath);
                if (!Directory.Exists(appSettingsDirectory))
                    Directory.CreateDirectory(appSettingsDirectory);

                settings = await appSettingsManager.LoadSettingsAsync().ConfigureAwait(true);

                var removeEmptyHotkeysProc = new RemoveEmptyHotkeysProc(hotkeySettingsManager);
                await removeEmptyHotkeysProc.Run();

                // Init localization
                var culture = new CultureInfo(settings.SelectedUiLanguage);
                TranslationSource.Instance.CurrentCulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
            }
            catch (Exception exp)
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("Unexpected Error 1 in App.Application_Startup()");
                sb.AppendLine($"   Message:{exp.Message}");
                sb.AppendLine($"StackTrace:{exp.StackTrace}");
                sb.AppendLine();
                _log.WriteLine(sb.ToString(), LogErrorSeverity.Error);
            }

            // Due to implementation constraints, taskbar icon must be instantiated as late as possible
            Container.RegisterInstance(FindResource("TaskbarIcon") as TaskbarIcon, new ContainerControlledLifetimeManager());
            Container.Resolve<ITaskbarIconManager>();
            _log.WriteLine("Resolve DI container");
            _startupHelper = Container.Resolve<IStartupHelper>();
            _workstationManager = Container.Resolve<IWorkstationManager>();

            _metaMessenger = Container.Resolve<IMetaPubSub>();

            _metaMessenger.Subscribe<ConnectedToServerEvent>(OnConnectedToServer);

            _serviceWatchdog = Container.Resolve<IServiceWatchdog>();
            _serviceWatchdog.Start();
            _deviceManager = Container.Resolve<IDeviceManager>();
            _userActionHandler = Container.Resolve<UserActionHandler>();
            _hotkeyModule = Container.Resolve<HotkeyModule>();
            await _metaMessenger.Publish(new EnableHotkeyManagerMessage());
            _buttonManager = Container.Resolve<IButtonManager>();
            _buttonManager.Enabled = true;
            _messageWindow = Container.Resolve<MessageWindow>();
            Container.Resolve<IProximityLockManager>();
            Container.Resolve<IVaultLowBatteryMonitor>();
            _bleDeviceUnpairHelper = Container.Resolve<BleDeviceUnpairHelper>();
            Container.Resolve<IAppUpdater>();
            Container.Resolve<IHideDialogsAdapter>();

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;


            // This will create an instance of password manager view model and allow handling of "Add new account" user action
            // It is required for subscribtion of PasswordManagerViewModel to the "AddAccountForApp" message
            // Note: PasswordManagerViewModel is not required for simplified UI
            Container.Resolve<PasswordManagerViewModel>();

            // Public Suffix list loading and updating may take some time (more than 8000 entries)
            // Better to load it before its required (for main domain extraction)
            await Task.Run(URLHelper.PreloadPublicSuffixAsync);

            // Handle first launch
            if (settings.IsFirstLaunch)
            {
                OnFirstLaunch();

                settings.IsFirstLaunch = false;
                appSettingsManager.SaveSettings(settings);
            }

            _windowsManager = Container.Resolve<IWindowsManager>();
            await _windowsManager.InitializeMainWindowAsync();
            await _metaMessenger.TryConnectToServer("HideezServicePipe");
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            _metaMessenger.Publish(new SessionSwitchMessage(Process.GetCurrentProcess().SessionId, e.Reason));
        }

        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        private static void Main()
        {
            if (SingleInstance<App>.InitializeAsFirstInstance("{EB9E0C35-8DC5-459D-80C2-93DCE0036C91}"))
            {
                var application = new App();
                application.InitializeComponent();
                application.Run();

                // Allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
        }

        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            // handle command line arguments of second instance
            // ...

            _log.WriteLine("Handle start of second instance");
            _windowsManager.ActivateMainWindow();

            
            return true;
        }

        private void OnFirstLaunch()
        {
            _log.WriteLine("First Hideez Client launch");
        }

        private void InitializeDIContainer()
        {
            Container = new UnityContainer();
#if DEBUG
            Container.AddExtension(new Diagnostic());
#endif
            _log.WriteLine("Start initialize DI container");

            #region ViewModels

            Container.RegisterType<MainViewModel>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IndicatorsViewModel>();
            Container.RegisterType<NotificationsContainerViewModel>();
            Container.RegisterType<DeviceNotAuthorizedNotificationViewModel>();
            Container.RegisterType<PinViewModel>();
            Container.RegisterType<MasterPasswordViewModel>();
            Container.RegisterType<HelpPageViewModel>();
            Container.RegisterType<SettingsPageViewModel>();
            Container.RegisterType<PasswordManagerViewModel>(new ContainerControlledLifetimeManager());
            Container.RegisterType<DeviceSettingsPageViewModel>(new ContainerControlledLifetimeManager());
            Container.RegisterType<ServerAddressEditControlViewModel>(new ContainerControlledLifetimeManager());
            Container.RegisterType<ServiceViewModel>(new ContainerControlledLifetimeManager());
            Container.RegisterType<DefaultPageViewModel>();
            Container.RegisterType<HardwareKeyPageViewModel>();
            Container.RegisterType<SoftwareKeyPageViewModel>();
            Container.RegisterType<SoftwareUnlockSettingViewModel>(new ContainerControlledLifetimeManager());
            Container.RegisterType<ActivationViewModel>();
            Container.RegisterType<VaultAccessSettingsControlViewModel>(new ContainerControlledLifetimeManager());
            Container.RegisterType<HotkeySettingsViewModel>(new ContainerControlledLifetimeManager());
            
            #endregion ViewModels

            Container.RegisterType<IStartupHelper, StartupHelper>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IWorkstationManager, WorkstationManager>(new ContainerControlledLifetimeManager());

            Container.RegisterType<ILog, NLogWrapper>(new ContainerControlledLifetimeManager());

            Container.RegisterType<IWindowsManager, WindowsManager>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IHideDialogsAdapter, HideDialogsAdapter>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IAppHelper, AppHelper>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IFileSerializer, XmlFileSerializer>();
            Container.RegisterType<IApplicationModeProvider, ApplicationModeRegistryProvider>(new ContainerControlledLifetimeManager(),
                new InjectionConstructor(HideezClientRegistryRoot.GetRootRegistryKey(false), typeof(ILog)));
            Container.RegisterType<IDeviceManager, DeviceManager>(new ContainerControlledLifetimeManager());
            Container.RegisterType<ISupportMailContentGenerator, SupportMailContentGenerator>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IButtonManager, ButtonManager>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IActiveDevice, ActiveDevice>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IWorkstationIdProvider, WorkstationIdProvider>(new ContainerControlledLifetimeManager(),
                new InjectionConstructor(HideezClientRegistryRoot.GetRootRegistryKey(false), typeof(ILog)));
            Container.RegisterType<IWorkstationInfoProvider, WorkstationInfoProvider>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IProximityLockManager, ProximityLockManager>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IVaultLowBatteryMonitor, VaultLowBatteryMonitor>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IConnectionModeProvider, ConnectionModeProvider>(new ContainerControlledLifetimeManager(),
                new InjectionConstructor(HideezClientRegistryRoot.GetRootRegistryKey(false), typeof(ILog)));
            Container.RegisterType<IHotkeyManager, HotkeyManager>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IHotkeySettingsController, HotkeySettingsController>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IHotkeyStatesMonitor, HotkeyStatesMonitor>(new ContainerControlledLifetimeManager());

            // Settings
            Container.RegisterType<ISettingsManager<ApplicationSettings>, HSSettingsManager<ApplicationSettings>>(new ContainerControlledLifetimeManager()
                , new InjectionConstructor(Path.Combine(Constants.DefaultSettingsFolderPath, Constants.ApplicationSettingsFileName), typeof(IFileSerializer), typeof(IMetaPubSub)));
            Container.RegisterType<ISettingsManager<HotkeySettings>, HSSettingsManager<HotkeySettings>>(new ContainerControlledLifetimeManager()
                , new InjectionConstructor(Path.Combine(Constants.DefaultSettingsFolderPath, Constants.HotkeySettingsFileName), typeof(IFileSerializer), typeof(IMetaPubSub)));

            // Service
            Container.RegisterType<IServiceProxy, ServiceProxy>(new ContainerControlledLifetimeManager());
            Container.RegisterInstance<IMetaPubSub>(new MetaPubSub(new MetaPubSubLogger(Container.Resolve<ILog>()))); // Todo: fix this idiocity
            Container.RegisterType<IServiceWatchdog, ServiceWatchdog>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IRemoteDeviceFactory, RemoteDeviceFactory>(new ContainerControlledLifetimeManager());

            // Taskbar icon
            Container.RegisterType<IBalloonTipNotifyManager, BalloonTipNotifyManager>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IMenuFactory, MenuFactory>(new ContainerControlledLifetimeManager());
            Container.RegisterType<TaskbarIconViewModel>(new ContainerControlledLifetimeManager());
            Container.RegisterType<ITaskbarIconManager, TaskbarIconManager>(new ContainerControlledLifetimeManager());

            // Input
            Container.RegisterType<UserActionHandler>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IInputCache, InputCache>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IInputHandler, InputHandler>(new ContainerControlledLifetimeManager());
            Container.RegisterType<IInputSimulator, InputSimulator>(new ContainerControlledLifetimeManager());
            Container.RegisterInstance(typeof(ITemporaryCacheAccount), new TemporaryCacheAccount(TimeSpan.FromMinutes(1)), new ContainerControlledLifetimeManager());
            Container.RegisterType<InputOtp>();
            Container.RegisterType<InputPassword>();
            Container.RegisterType<InputLogin>();

            // QrScanner
            // Note: Previous BarcodeReader was renamed into BarcodeReaderGeneric when 
            // ZXing library got updated; Its AutoRotate property was set to True in constructor
            Container.RegisterType<IBarcodeReader, BarcodeReader>(new ContainerControlledLifetimeManager()); 
            Container.RegisterType<IQrScannerHelper, QrScannerHelper>();

            Container.RegisterType<INotificationsManager, NotificationsManager>(new ContainerControlledLifetimeManager());

            Container.RegisterType<IEventPublisher, EventPublisher>(new ContainerControlledLifetimeManager());

            Container.RegisterType<MessageWindow>(new ContainerControlledLifetimeManager());

            Container.RegisterType<IAppUpdater, AppUpdater>(new ContainerControlledLifetimeManager());

            _log.WriteLine("Finish initialize DI container");

        }

        /// <summary>
        /// Check if configuration file can be opened. File is deleted on error.
        /// </summary>
        private void HandleBrokenConfig()
        {
            try
            {
                ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            }
            catch (ConfigurationErrorsException ex)
            {
                string filename = ex.Filename;

                if (File.Exists(filename))
                    File.Delete(filename);
            }
        }

        void Cleanup()
        {
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;

            // Unity-Container call Dispose on all instances implementing the IDisposable interface registered by ContainerControlledLifetimeManager or HierarchicalLifetimeManager.
            Container.Dispose();
            LogManager.Flush();
            LogManager.Shutdown();
        }

        /// <summary>
        /// Called when MetaPubSub was connected to a server on the service side
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        async Task OnConnectedToServer(ConnectedToServerEvent arg)
        {
            await _metaMessenger.PublishOnServer(new LoginClientRequestMessage());
        }
    }
}
