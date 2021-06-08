using Hideez.CsrBLE;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity;
using Hideez.SDK.Communication.Proximity.Interfaces;
using Hideez.SDK.Communication.Workstation;
using HideezMiddleware;
using HideezMiddleware.Audit;
using HideezMiddleware.ClientManagement;
using HideezMiddleware.ConnectionModeProvider;
using HideezMiddleware.CredentialProvider;
using HideezMiddleware.DeviceConnection;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.Dongle;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.Other;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.WinBle;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.DeviceLogging;
using HideezMiddleware.IPC.DTO;
using HideezMiddleware.Local;
using HideezMiddleware.Localize;
using HideezMiddleware.Modules;
using HideezMiddleware.Modules.Audit;
using HideezMiddleware.Modules.ClientPipe;
using HideezMiddleware.Modules.CredentialProvider;
using HideezMiddleware.Modules.Csr;
using HideezMiddleware.Modules.DeviceManagement;
using HideezMiddleware.Modules.FatalExceptionHandler;
using HideezMiddleware.Modules.Hes;
using HideezMiddleware.Modules.Hes.Messages;
using HideezMiddleware.Modules.HSServiceCheck;
using HideezMiddleware.Modules.ReconnectAndWorkstationLock;
using HideezMiddleware.Modules.RemoteUnlock;
using HideezMiddleware.Modules.Rfid;
using HideezMiddleware.Modules.ServiceEvents;
using HideezMiddleware.Modules.UpdateCheck;
using HideezMiddleware.Modules.WinBle;
using HideezMiddleware.ReconnectManager;
using HideezMiddleware.Resources;
using HideezMiddleware.ScreenActivation;
using HideezMiddleware.Settings;
using HideezMiddleware.Settings.SettingsProvider;
using HideezMiddleware.Utils.WorkstationHelper;
using HideezMiddleware.Workstation;
using Meta.Lib.Modules.PubSub;
using ServiceLibrary.Implementation.ScreenActivation;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using Unity.Resolution;
using WinBle;

namespace ServiceLibrary.Implementation
{
    /// <summary>
    /// Allows flexible building of Hideez Service by separately enabling required modules. 
    /// Modules are initialized and enabled as soon as they were added.
    /// </summary>
    public sealed class HideezServiceBuilder
    {
        /// <summary>
        /// Reference to service instance that is being built
        /// </summary>
        private HideezService _service;

        /// <summary>
        /// Container that holds all modules previously created for the service and used for params injection
        /// </summary>
        private IUnityContainer _container;

        // Stored reference to %ProgramData% folder to shorten filepath declarations
        readonly string _commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        
        private void Cleanup()
        {
            _service = null;
            _container = null;
        }

        private void AddModule(IModule module)
        {
            _service.ServiceModules.Add(module);
        }

        /// <summary>
        /// Start building service by creating the essential modules
        /// </summary>
        public void Begin()
        {
            _container = new UnityContainer();
            _service = new HideezService(_container);
            _container.AddExtension(new Diagnostic());

            // Register and instantiate essentials

            // Used by all modules to write logs
            NLog.LogManager.EnableLogging();
            var sdkLogger = new NLogWrapper();
            _container.RegisterInstance<ILog>(sdkLogger, new ContainerControlledLifetimeManager());

            // Used by almost all modules to communicate with each other through "weak-event style" messages
            var messenger = new MetaPubSub(new MetaPubSubLogger(sdkLogger));
            _container.RegisterInstance<IMetaPubSub>(messenger, new ContainerControlledLifetimeManager());

            var serviceEventsModule = _container.Resolve<ServiceEventsModule>();
            AddModule(serviceEventsModule);

            // Used by some modules to access data stored in registry
            var registryRoot = HideezClientRegistryRoot.GetRootRegistryKey(true);
            _container.RegisterInstance(registryRoot, new ContainerControlledLifetimeManager());

            _container.RegisterType<IConnectionModeProvider, ConnectionModeProvider>(new ContainerControlledLifetimeManager());

            // Used by many modules to gather info about currect PC state
            _container.RegisterType<IWorkstationHelper, WorkstationHelper>(new ContainerControlledLifetimeManager());

            // Used by some modules to gather info about workstation
            _container.RegisterType<IWorkstationIdProvider, WorkstationIdProvider>(new ContainerControlledLifetimeManager());
            _container.RegisterType<IWorkstationInfoProvider, WorkstationInfoProvider>(new ContainerControlledLifetimeManager());

            // Device manager module is the most basic one and will be required for most configurations
            _container.RegisterType<ConnectionManagersCoordinator>(new ContainerControlledLifetimeManager());
            _container.RegisterType<ConnectionManagerRestarter>(new ContainerControlledLifetimeManager());
            _container.RegisterType<DeviceManager>(new ContainerControlledLifetimeManager());
            var deviceManagerModule = _container.Resolve<DeviceManagementModule>();
            AddModule(deviceManagerModule);

            // Settings are used to configure operation of many modules
            string settingsDirectory = $@"{_commonAppData}\Hideez\Service\Settings\";
            string bondsFolderPath = $"{_commonAppData}\\Hideez\\Service\\Bonds";
            string deviceLogsPath = $"{_commonAppData}\\Hideez\\Service\\DeviceLogs";
            string sdkSettingsPath = Path.Combine(settingsDirectory, "Sdk.xml");
            string rfidSettingsPath = Path.Combine(settingsDirectory, "Rfid.xml");
            string proximitySettingsPath = Path.Combine(settingsDirectory, "Unlock.xml");
            string serviceSettingsPath = Path.Combine(settingsDirectory, "Service.xml");
            string workstationSettingsPath = Path.Combine(settingsDirectory, "Workstation.xml");
            string userProximitySettingsPath = Path.Combine(settingsDirectory, "UserProximitySettings.xml");

            Directory.CreateDirectory(bondsFolderPath); // Ensure directory for bonds is created since unmanaged code doesn't do that

            IFileSerializer fileSerializer = _container.Resolve<XmlFileSerializer>();

            List<Task> settingsInitializationTasks = new List<Task>
            {
                Task.Run(async () =>
                {
                    var sdkSettingsManager = new SettingsManager<SdkSettings>(sdkSettingsPath, fileSerializer);
                    sdkSettingsManager.InitializeFileStruct();
                    await SdkConfigLoader.LoadSdkConfigFromFileAsync(sdkSettingsManager).ConfigureAwait(false);
                    sdkLogger.WriteLine(nameof(HideezService), $"{nameof(SdkSettings)} loaded");
                    _container.RegisterInstance<ISettingsManager<SdkSettings>>(sdkSettingsManager, new ContainerControlledLifetimeManager());
                }),
                Task.Run(async () =>
                {
                    var rfidSettingsManager = new SettingsManager<RfidSettings>(rfidSettingsPath, fileSerializer);
                    rfidSettingsManager.InitializeFileStruct();
                    await rfidSettingsManager.LoadSettingsAsync().ConfigureAwait(false);
                    sdkLogger.WriteLine(nameof(HideezService), $"{nameof(RfidSettings)} loaded");
                    _container.RegisterInstance<ISettingsManager<RfidSettings>>(rfidSettingsManager, new ContainerControlledLifetimeManager());

                    messenger.Subscribe<HesAppConnection_HUbRFIDIndicatorStateArrivedMessage>(async (msg) =>
                    {
                        var settings = await rfidSettingsManager.GetSettingsAsync().ConfigureAwait(false);
                        settings.IsRfidEnabled = msg.IsEnabled;
                        rfidSettingsManager.SaveSettings(settings);
                    });
                }),
                Task.Run(async () =>
                {
                    var proximitySettingsManager = new SettingsManager<ProximitySettings>(proximitySettingsPath, fileSerializer);
                    proximitySettingsManager.InitializeFileStruct();
                    await proximitySettingsManager.GetSettingsAsync().ConfigureAwait(false);
                    _container.RegisterInstance<ISettingsManager<ProximitySettings>>(proximitySettingsManager, new ContainerControlledLifetimeManager());
                    sdkLogger.WriteLine(nameof(HideezService), $"{nameof(ProximitySettings)} loaded");

                    messenger.Subscribe<HesAppConnection_HubProximitySettingsArrivedMessage>(async (msg) =>
                    {
                        var settings = await proximitySettingsManager.GetSettingsAsync().ConfigureAwait(false);
                        settings.DevicesProximity = msg.Settings.ToArray();
                        proximitySettingsManager.SaveSettings(settings);
                    });
                }),
                Task.Run(async () =>
                {
                    var serviceSettingsManager = new SettingsManager<ServiceSettings>(serviceSettingsPath, fileSerializer);
                    serviceSettingsManager.InitializeFileStruct();
                    await serviceSettingsManager.LoadSettingsAsync().ConfigureAwait(false);
                    _container.RegisterInstance<ISettingsManager<ServiceSettings>>(serviceSettingsManager, new ContainerControlledLifetimeManager());
                    sdkLogger.WriteLine(nameof(HideezService), $"{nameof(ServiceSettings)} loaded");

                    messenger.Subscribe<HesAppConnection_AlarmMessage>(async (msg) =>
                    {
                        var settings = await serviceSettingsManager.GetSettingsAsync().ConfigureAwait(false);
                        settings.AlarmTurnOn = msg.IsEnabled;
                        serviceSettingsManager.SaveSettings(settings);
                    });
                }),
                Task.Run(async () =>
                {
                    var workstationSettingsManager = new WatchingSettingsManager<WorkstationSettings>(workstationSettingsPath, fileSerializer);
                    workstationSettingsManager.InitializeFileStruct();
                    await workstationSettingsManager.LoadSettingsAsync().ConfigureAwait(false);
                    workstationSettingsManager.AutoReloadOnFileChanges = true;
                    sdkLogger.WriteLine(nameof(HideezService), $"{nameof(WorkstationSettings)} loaded");
                    _container.RegisterInstance<ISettingsManager<WorkstationSettings>>(workstationSettingsManager, new ContainerControlledLifetimeManager());
                }),
                Task.Run(async () =>
                {
                    var userProximitySettingsManager = new WatchingSettingsManager<UserProximitySettings>(userProximitySettingsPath, fileSerializer);
                    userProximitySettingsManager.InitializeFileStruct();
                    await userProximitySettingsManager.LoadSettingsAsync().ConfigureAwait(false);
                    userProximitySettingsManager.AutoReloadOnFileChanges = true;
                    sdkLogger.WriteLine(nameof(HideezService), $"{nameof(UserProximitySettings)} loaded");
                    _container.RegisterInstance<ISettingsManager<UserProximitySettings>>(userProximitySettingsManager, new ContainerControlledLifetimeManager());
                })
            };

            Task.WhenAll(settingsInitializationTasks).Wait();

            // Used by connection processors and connection flow
            _container.RegisterType<IScreenActivator, MetalibScreenActivator>(new ContainerControlledLifetimeManager());

            // Facade that is used by some modules to sent notifications to UI
            var cp = _container.Resolve<CredentialProviderProxy>();
            _container.RegisterInstance(cp);
            _container.RegisterInstance<IWorkstationUnlocker>(cp);

            _container.RegisterType<ServiceClientUiManager>(new ContainerControlledLifetimeManager());
            _container.RegisterType<IClientUiManager, UiProxyManager>(new ContainerControlledLifetimeManager(), 
                new InjectionConstructor(
                    typeof(CredentialProviderProxy),
                    typeof(ServiceClientUiManager),
                    typeof(ILog)
                    ));

            // Status manager tracks status of certain modules and caches it for client modules, like ClientPipe and CP
            var statusManager = _container.Resolve<StatusManager>();
            _container.RegisterInstance(statusManager, new ContainerControlledLifetimeManager());

            // Credential provider module is currently essentia
            var credentialProviderModule = _container.Resolve<CredentialProviderModule>();
            AddModule(credentialProviderModule);

            // Provides local storage for device information
            _container.RegisterType<ILocalDeviceInfoCache, LocalDeviceInfoCache>(new ContainerControlledLifetimeManager());

            // Provides interface to read device logs
            _container.RegisterType<IDeviceLogWriter, DeviceLogWriter>(new ContainerControlledLifetimeManager());
            _container.RegisterType<DeviceLogManager>(new ContainerControlledLifetimeManager(), 
                new InjectionConstructor(
                    deviceLogsPath,
                    typeof(IDeviceLogWriter),
                    typeof(ISettingsManager<ServiceSettings>),
                    typeof(ILog)
                    ));

            // Provides interface to manage csr bonds
            _container.RegisterType<BondManager>(new ContainerControlledLifetimeManager(),
                new InjectionConstructor(
                    bondsFolderPath,
                    typeof(ILog)
                    ));

            // Connection managers are registered for here convenience
            _container.RegisterType<WinBleConnectionManager>(new ContainerControlledLifetimeManager());
            _container.RegisterType<BleConnectionManager>(new ContainerControlledLifetimeManager(), 
                new InjectionConstructor(
                    typeof(ILog),
                    bondsFolderPath
                    ));

            // Hes access manager is used by workflow and processors
            _container.RegisterType<IHesAccessManager, HesAccessManager>(new ContainerControlledLifetimeManager());
        }

        public void AddEnterpriseProximitySettingsSupport()
        {
            _container.RegisterType<IDeviceProximitySettingsProvider, EnterpriseProximitySettingsProvider>(new ContainerControlledLifetimeManager());
        }

        public void AddStandaloneProximitySettingsSupport()
        {
            _container.RegisterType<IDeviceProximitySettingsProvider, UserProximitySettingsProvider>(new ContainerControlledLifetimeManager());
        }

        public void AddEnterpriseResources()
        {
            ResourceManagersProvider.SetResources(typeof(Strings));
            ResourceManagersProvider.SetErrorCodesResources(typeof(ErrorCode));
        }

        public void AddStandaloneResources()
        {
            ResourceManagersProvider.SetResources(typeof(Strings), typeof(HideezMiddleware.Resources.Standalone.Strings));
            ResourceManagersProvider.SetErrorCodesResources(typeof(ErrorCode), typeof(HideezMiddleware.Resources.Standalone.ErrorCode));
        }

        public void AddFatalExceptionHandling()
        {
            var fatalExceptionHandlerModule = _container.Resolve<FatalExceptionHandlerModule>();
            AddModule(fatalExceptionHandlerModule);
        }

        public void AddHES()
        {
            _container.RegisterType<IHesAppConnection, HesAppConnection>(new ContainerControlledLifetimeManager(), 
                new InjectionConstructor(
                    typeof(DeviceManager),
                    typeof(IWorkstationInfoProvider),
                    typeof(IHesAccessManager),
                    typeof(ILog)
                    ));
            var hesModule = _container.Resolve<HesModule>();
            AddModule(hesModule);
        }

        public void AddRemoteUnlock()
        {
            _container.RegisterType<IHesAppConnection, HesAppConnection>("tb", new ContainerControlledLifetimeManager(),
                new InjectionConstructor(
                    typeof(IWorkstationInfoProvider),
                    typeof(ILog)
                    ));
            var remoteUnlockModule = _container.Resolve<RemoteUnlockModule>(
                new ParameterOverride(typeof(IHesAppConnection), _container.Resolve<IHesAppConnection>("tb")));
            AddModule(remoteUnlockModule);
        }

        public void AddClientPipe()
        {
            var module = _container.Resolve<ClientPipeModule>();
            AddModule(module);
        }

        public void AddEnterpriseConnectionFlow()
        {
            var connectionFlowProcessorfactory = _container.Resolve<EnterpriseConnectionFlowProcessorFactory>();
            var connectionFlowProcessor = connectionFlowProcessorfactory.Create();

            _container.RegisterInstance<ConnectionFlowProcessorBase>(connectionFlowProcessor, new ContainerControlledLifetimeManager());
        }

        public void AddStandaloneConnectionFlow()
        {
            var connectionFlowProcessorfactory = _container.Resolve<StandaloneConnectionFlowProcessorFactory>();
            var standaloneConnectionFlowProcessor = connectionFlowProcessorfactory.Create();

            _container.RegisterInstance<ConnectionFlowProcessorBase>(standaloneConnectionFlowProcessor, new ContainerControlledLifetimeManager());
        }

        /// <summary>
        /// Enable audit events generation and saving
        /// </summary>
        public void AddAudit()
        {
            string auditEventsDirectoryPath = $@"{_commonAppData}\Hideez\Service\WorkstationEvents\";
            var sessionTimestampPath = $@"{_commonAppData}\Hideez\Service\Timestamp\timestamp.dat";

            _container.RegisterType<ISessionInfoProvider, SessionInfoProvider>(new ContainerControlledLifetimeManager());
            _container.RegisterType<EventSaver>(new ContainerControlledLifetimeManager(),
                new InjectionConstructor(
                    typeof(ISessionInfoProvider),
                    typeof(IWorkstationIdProvider),
                    auditEventsDirectoryPath,
                    typeof(ILog)
                    ));
            _container.RegisterType<SessionTimestampLogger>(new ContainerControlledLifetimeManager(),
                new InjectionConstructor(
                    sessionTimestampPath,
                    typeof(ISessionInfoProvider),
                    typeof(EventSaver),
                    typeof(IWorkstationHelper),
                    typeof(ILog)
                    ));
            _container.RegisterType<EventSender>();

            // TODO: These classes must be decoupled from unlock processors and added to AuditModule
            //var sessionUnlockMethodMonitor = _container.Resolve<SessionUnlockMethodMonitor>();
            //AddModule(sessionUnlockMethodMonitor);
            //
            //var sessionSwitchLogger = _container.Resolve<SessionSwitchLogger>();
            //AddModule(sessionSwitchLogger);

            var auditModule = _container.Resolve<AuditModule>();
            AddModule(auditModule);
        }

        // todo: simplify resolution
        public void AddDongleSupport()
        {
            var log = _container.Resolve<ILog>();
            var messenger = _container.Resolve<IMetaPubSub>();
            var csrBleConnectionManager = _container.Resolve<BleConnectionManager>();
            var connectionFlow = _container.Resolve<ConnectionFlowProcessorBase>();
            var proximitySettingsProvider = _container.Resolve<IDeviceProximitySettingsProvider>();
            var advIgnoreCsrList = new AdvertisementIgnoreList(csrBleConnectionManager, proximitySettingsProvider, SdkConfig.DefaultLockTimeout, log);
            var deviceManager = _container.Resolve<DeviceManager>();
            var credentialProviderProxy = _container.Resolve<CredentialProviderProxy>();
            var uiManager = _container.Resolve<IClientUiManager>();

            var tapConnectionProcessor = new TapConnectionProcessor(connectionFlow, csrBleConnectionManager, messenger, log);
            var activityConnectionProcessor = new ActivityConnectionProcessor(
                connectionFlow,
                csrBleConnectionManager,
                proximitySettingsProvider,
                advIgnoreCsrList,
                deviceManager,
                credentialProviderProxy,
                uiManager,
                messenger,
                log);
            var proximityConnectionProcessor = new ProximityConnectionProcessor(
                connectionFlow,
                csrBleConnectionManager,
                proximitySettingsProvider,
                advIgnoreCsrList,
                deviceManager,
                credentialProviderProxy,
                messenger,
                log);

            var connectionManagersCoordinator = _container.Resolve<ConnectionManagersCoordinator>();
            var connectionManagersRestarter = _container.Resolve<ConnectionManagerRestarter>(); 
            var csrModule = new CsrModule(
                connectionManagersCoordinator,
                connectionManagersRestarter,
                advIgnoreCsrList,
                csrBleConnectionManager,
                tapConnectionProcessor,
                activityConnectionProcessor,
                proximityConnectionProcessor,
                messenger,
                log);
            AddModule(csrModule);
        }

        // todo: simplify resolution
        public void AddWinBleSupport()
        {
            var log = _container.Resolve<ILog>();
            var messenger = _container.Resolve<IMetaPubSub>();
            var connectionFlow = _container.Resolve<ConnectionFlowProcessorBase>();
            var winBleConnectionManager = _container.Resolve<WinBleConnectionManager>();
            var winBleConnectionManagerWrapper = new WinBleConnectionManagerWrapper(winBleConnectionManager, log);
            winBleConnectionManager.UnpairProvider = new UnpairProvider(messenger, log);
            var proximitySettingsProvider = _container.Resolve<IDeviceProximitySettingsProvider>();
            // WinBle rssi messages arrive much less frequently than when using csr. Empirically calculated 20s rssi clear delay to be acceptable.
            var advIgnoreWinBleList = new AdvertisementIgnoreList(winBleConnectionManagerWrapper, proximitySettingsProvider, 20, log);
            var deviceManager = _container.Resolve<DeviceManager>();
            var credentialProviderProxy = _container.Resolve<CredentialProviderProxy>();
            var uiManager = _container.Resolve<IClientUiManager>();
            var workstationHelper = _container.Resolve<IWorkstationHelper>();

            var commandLinkConnectionProcessor = new CommandLinkConnectionProcessor(
                connectionFlow,
                winBleConnectionManagerWrapper,
                proximitySettingsProvider,
                advIgnoreWinBleList,
                deviceManager,
                credentialProviderProxy,
                uiManager,
                messenger,
                log);
            var activityConnectionProcessor = new ActivityConnectionProcessor(
                connectionFlow,
                winBleConnectionManagerWrapper,
                proximitySettingsProvider,
                advIgnoreWinBleList,
                deviceManager,
                credentialProviderProxy,
                uiManager,
                messenger,
                log);
            var automaticConnectionProcessor = new AutomaticConnectionProcessor(
                connectionFlow, 
                winBleConnectionManagerWrapper,
                advIgnoreWinBleList,
                proximitySettingsProvider, 
                deviceManager, 
                credentialProviderProxy, 
                uiManager,
                workstationHelper,
                messenger,
                log);

            var commandLinkVisibilityController = new CommandLinkVisibilityController(
                credentialProviderProxy,
                winBleConnectionManager,
                connectionFlow, 
                log);


            var connectionManagersCoordinator = _container.Resolve<ConnectionManagersCoordinator>();
            var connectionManagersRestarter = _container.Resolve<ConnectionManagerRestarter>();
            var winBleModule = new WinBleModule(
                connectionManagersCoordinator,
                connectionManagersRestarter,
                advIgnoreWinBleList,
                winBleConnectionManagerWrapper,
                commandLinkConnectionProcessor,
                activityConnectionProcessor,
                automaticConnectionProcessor,
                commandLinkVisibilityController,
                messenger,
                log);
            AddModule(winBleModule);
        }

        public void AddRfidSupport()
        {
            _container.RegisterType<RfidServiceConnection>();
            _container.RegisterType<RfidConnectionProcessor>();

            var rfidModule = _container.Resolve<RfidModule>();
            AddModule(rfidModule);
        }

        public void AddWorkstationLock()
        {
            _container.RegisterType<ProximityMonitorManager>(new ContainerControlledLifetimeManager());
            _container.RegisterType<DeviceReconnectManager>(new ContainerControlledLifetimeManager());
            _container.RegisterType<WorkstationLockProcessor>(new ContainerControlledLifetimeManager());
            var workstationLockModule = _container.Resolve<WorkstationLockModule>();
            AddModule(workstationLockModule);
        }

        public void AddUpdateCheck()
        {
            var applicationUpdateModule = _container.Resolve<UpdateCheckModule>();
            AddModule(applicationUpdateModule);
        }

        public void AddHSServiceCheck()
        {
            var hsServiceCheckModule = _container.Resolve<HSServiceCheckModule>();
            AddModule(hsServiceCheckModule);
        }

        public void End()
        {
            var connectionManagersCoordinator = _container.Resolve<ConnectionManagersCoordinator>();
            connectionManagersCoordinator.Start();

            var connectionManagersRestarter = _container.Resolve<ConnectionManagerRestarter>();
            connectionManagersRestarter.Start();
        }

        /// <summary>
        /// Finishes building, returns built service instance and performs builder cleanup
        /// </summary>
        public HideezService GetService()
        {
            var finishedService = _service;

            Cleanup();

            return finishedService;
        }
    }
}
