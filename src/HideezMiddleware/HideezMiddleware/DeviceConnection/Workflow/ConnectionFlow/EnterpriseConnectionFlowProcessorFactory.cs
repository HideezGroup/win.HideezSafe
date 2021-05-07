using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.Local;
using HideezMiddleware.ScreenActivation;
using HideezMiddleware.Settings;
using Hideez.SDK.Communication.Device;
using HideezMiddleware.Utils.WorkstationHelper;
using HideezMiddleware.DeviceLogging;
using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow
{
    public class EnterpriseConnectionFlowProcessorFactory
    {
        private readonly DeviceManager _deviceManager;
        private readonly BondManager _bondManager;
        private readonly IHesAppConnection _hesConnection;
        private readonly IWorkstationUnlocker _workstationUnlocker;
        private readonly IScreenActivator _screenActivator;
        private readonly IClientUiManager _ui;
        private readonly IHesAccessManager _hesAccessManager;
        private readonly ISettingsManager<ServiceSettings> _serviceSettingsManager;
        private readonly ILocalDeviceInfoCache _localDeviceInfoCache;
        private readonly IWorkstationHelper _workstationHelper;
        private readonly DeviceLogManager _deviceLogManager;
        private readonly IMetaPubSub _messenger;
        private readonly ILog _log;

        public EnterpriseConnectionFlowProcessorFactory(
            DeviceManager deviceManager,
            BondManager bondManager,
            IHesAppConnection hesConnection,
            IWorkstationUnlocker workstationUnlocker,
            IScreenActivator screenActivator,
            IClientUiManager ui,
            IHesAccessManager hesAccessManager,
            ISettingsManager<ServiceSettings> serviceSettingsManager,
            ILocalDeviceInfoCache localDeviceInfoCache,
            IWorkstationHelper workstationHelper,
            DeviceLogManager deviceLogManager,
            IMetaPubSub messenger,
            ILog log)
        {
            _deviceManager = deviceManager;
            _bondManager = bondManager;
            _hesConnection = hesConnection;
            _workstationUnlocker = workstationUnlocker;
            _screenActivator = screenActivator;
            _ui = ui;
            _hesAccessManager = hesAccessManager;
            _serviceSettingsManager = serviceSettingsManager;
            _localDeviceInfoCache = localDeviceInfoCache;
            _workstationHelper = workstationHelper;
            _deviceLogManager = deviceLogManager;
            _messenger = messenger;
            _log = log;
        }

        public EnterpriseConnectionFlowProcessor Create()
        {
            var flowSubprocessors = new EnterpriseConnectionFlowProcessor.ConnectionFlowSubprocessorsStruct()
            {
                PermissionsCheckProcessor = new PermissionsCheckProcessor(_hesAccessManager, _serviceSettingsManager),
                VaultConnectionProcessor = new VaultConnectionProcessor(_ui, _bondManager, _deviceManager, _log),
                LicensingProcessor = new LicensingProcessor(_hesConnection, _ui, _log),
                StateUpdateProcessor = new StateUpdateProcessor(_hesConnection, _log),
                ActivationProcessor = new ActivationProcessor(_hesConnection, _ui, _log),
                AccountsUpdateProcessor = new AccountsUpdateProcessor(_hesConnection, _log),
                MasterkeyProcessor = new VaultAuthorizationProcessor(_hesConnection, _ui, _log),
                UserAuthorizationProcessor = new UserAuthorizationProcessor(_ui, _log),
                UnlockProcessor = new UnlockProcessor(_ui, _workstationUnlocker, _log),
                CacheVaultInfoProcessor = new CacheVaultInfoProcessor(_localDeviceInfoCache, _log)
            };

            return new EnterpriseConnectionFlowProcessor(
                _deviceManager,
                _hesConnection,
                _workstationUnlocker,
                _screenActivator,
                _ui,
                _hesAccessManager,
                _serviceSettingsManager,
                flowSubprocessors,
                _workstationHelper,
                _deviceLogManager,
                _messenger,
                _log);
        }
    }
}
