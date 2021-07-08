using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.ClientManagement;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.IPC.DTO;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.Localize;
using HideezMiddleware.Modules.DeviceManagement.Messages;
using HideezMiddleware.Modules.Hes.Messages;
using HideezMiddleware.Modules.ServiceEvents.Messages;
using HideezMiddleware.Settings;
using HideezMiddleware.Utils;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.ClientPipe
{
    public sealed class ClientPipeModule : ModuleBase
    {
        readonly DeviceManager _deviceManager;
        readonly ServiceClientUiManager _clientUi;
        readonly ISettingsManager<ServiceSettings> _serviceSettingsManager;
        readonly ISettingsManager<UserProximitySettings> _userProximtiySettingsManager;
        readonly SessionUnlockMethodMonitor _sessionUnlockMethodMonitor;
        readonly StatusManager _statusManager;
        readonly ConnectionFlowProcessorBase _connectionFlowProcessor;
        readonly PipeDeviceConnectionManager _pipeDeviceConnectionManager;

        public ClientPipeModule(DeviceManager deviceManager, 
            ServiceClientUiManager clientUiProxy, 
            ISettingsManager<ServiceSettings> serviceSettingsManager,
            ISettingsManager<UserProximitySettings> userProximtiySettingsManager,
            SessionUnlockMethodMonitor sessionUnlockMethodMonitor,
            StatusManager statusManager,
            ConnectionFlowProcessorBase connectionFlowProcessor,
            IMetaPubSub messenger, 
            ILog log)
            : base(messenger, nameof(ClientPipeModule), log)
        {
            _deviceManager = deviceManager;
            _clientUi = clientUiProxy;
            _serviceSettingsManager = serviceSettingsManager;
            _userProximtiySettingsManager = userProximtiySettingsManager;
            _sessionUnlockMethodMonitor = sessionUnlockMethodMonitor;
            _statusManager = statusManager;
            _connectionFlowProcessor = connectionFlowProcessor;
            _pipeDeviceConnectionManager = new PipeDeviceConnectionManager(deviceManager, messenger, log);

            var pipeName = "HideezServicePipe";
            _messenger.StartServer(pipeName, () =>
            {
                try
                {
                    WriteLine("Custom pipe config started");
                    var pipeSecurity = new PipeSecurity();
                    pipeSecurity.AddAccessRule(new PipeAccessRule(
                        new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                        PipeAccessRights.FullControl,
                        AccessControlType.Allow));

                    var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 32,
                        PipeTransmissionMode.Message, PipeOptions.Asynchronous, 4096, 4096, pipeSecurity);

                    WriteLine("Custom pipe config successful");
                    return pipe;
                }
                catch (Exception ex)
                {
                    WriteLine("Custom pipe config failed.", ex);
                    return null;
                }
            });

            _serviceSettingsManager.SettingsChanged += ServiceSettingsManager_SettingsChanged;

            _connectionFlowProcessor.DeviceFinishedMainFlow += ConnectionFlowProcessor_DeviceFinishedMainFlow;

            _messenger.Subscribe(GetSafeHandler<DeviceManager_DeviceAddedMessage>(OnDeviceAdded));
            _messenger.Subscribe(GetSafeHandler<DeviceManager_DeviceRemovedMessage>(OnDeviceRemoved));

            _messenger.Subscribe(GetSafeHandler<RemoteClientConnectedEvent>(OnClientConnected));
            _messenger.Subscribe(GetSafeHandler<RemoteClientDisconnectedEvent>(OnClientDisconnected));

            _messenger.Subscribe(GetSafeHandler<LoginClientRequestMessage>(OnClientLogin));
            _messenger.Subscribe(GetSafeHandler<RefreshServiceInfoMessage>(OnRefreshServiceInfo));
            _messenger.Subscribe(GetSafeHandler<Hideez.Integration.Lite.Messages.RefreshServiceInfoMessage>(OnIntegrationRefreshServiceInfo));

            _messenger.Subscribe(GetSafeHandler<HesAppConnection_LockHwVaultStorageMessage>(OnLockHwVaultStorage));
            _messenger.Subscribe(GetSafeHandler<HesAppConnection_LiftHwVaultStorageLockMessage>(OnLiftHwVaultStorageLock));
            _messenger.Subscribe(GetSafeHandler<HesAppConnection_HubConnectionStateChangedMessage>(OnHubConnectionStateChanged));

            _messenger.Subscribe(GetSafeHandler<SessionSwitchMonitor_SessionSwitchMessage>(OnSessionSwitch));
            _messenger.Subscribe(GetSafeHandler<LoadUserProximitySettingsMessage>(LoadUserProximitySettings));
            _messenger.Subscribe(GetSafeHandler<SaveUserProximitySettingsMessage>(SaveUserProximitySettings));
            _messenger.Subscribe(GetSafeHandler<RemoveUserProximitySettingsMessage>(RemoveUserProximitySettings));

            _messenger.Subscribe(GetSafeHandler<LanguageSettingsChangedMessage>(OnClientLanguageSettingsChanged));
        }

        private void Error(Exception ex, string message = "")
        {
            WriteLine(message, ex);
        }

        private void Error(string message)
        {
            WriteLine(message, LogErrorSeverity.Error);
        }

        private DeviceDTO[] GetDevices()
        {
            try
            {
                return _deviceManager.Devices.Select(d => new DeviceDTO(d)).ToArray();
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
            }
        }

        private Hideez.Integration.Lite.DTO.DeviceDTO[] GetDevicesForIntegration()
        {
            try
            {
                return _deviceManager.Devices.Select(d => new Hideez.Integration.Lite.DTO.DeviceDTO().Initialize(d)).ToArray();
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
            }
        }

        async Task RefreshServiceInfo()
        {
            await _statusManager.SendStatusToUI();

            await SafePublish(new DevicesCollectionChangedMessage(GetDevices()));
            await SafePublish(new Hideez.Integration.Lite.Messages.DevicesCollectionChangedMessage(GetDevicesForIntegration()));

            await SafePublish(new ServiceSettingsChangedMessage(_serviceSettingsManager.Settings.EnableSoftwareVaultUnlock, RegistrySettings.GetHesAddress(this)));

            await SafePublish(new RefreshStatusMessage());
        }

        async void ServiceSettingsManager_SettingsChanged(object sender, SettingsChangedEventArgs<ServiceSettings> e)
        {
            await RefreshServiceInfo();
        }

        async void ConnectionFlowProcessor_DeviceFinishedMainFlow(object sender, IDevice e)
        {
            await SafePublish(new DeviceFinishedMainFlowMessage(new DeviceDTO(e)));
        }

        private async Task OnDeviceAdded(DeviceManager_DeviceAddedMessage msg)
        {
            await SafePublish(new DevicesCollectionChangedMessage(GetDevices()));
            await SafePublish(new Hideez.Integration.Lite.Messages.DevicesCollectionChangedMessage(GetDevicesForIntegration()));
        }

        private async Task OnDeviceRemoved(DeviceManager_DeviceRemovedMessage msg)
        {
            await SafePublish(new DevicesCollectionChangedMessage(GetDevices()));
            await SafePublish(new Hideez.Integration.Lite.Messages.DevicesCollectionChangedMessage(GetDevicesForIntegration()));
        }

        Task OnClientConnected(RemoteClientConnectedEvent arg)
        {
            WriteLine($">>>>>> AttachClient, {arg.TotalClientsCount} remaining clients", LogErrorSeverity.Debug);
            return Task.CompletedTask;
        }

        Task OnClientDisconnected(RemoteClientDisconnectedEvent arg)
        {
            WriteLine($">>>>>> DetachClient, {arg.TotalClientsCount} remaining clients", LogErrorSeverity.Debug);
            return Task.CompletedTask;
        }

        // This message is sent by client when its ready to receive messages from server
        // Upon receiving LoginClientRequestMessage we sent to the client all currently available information
        // Previously the client had to ask for each bit separatelly. Now we instead send it upon connection establishment
        // The updates in sent information are sent separatelly
        async Task OnClientLogin(LoginClientRequestMessage arg)
        {
            WriteLine($"Service client login");

            TranslationSource.Instance.CurrentCulture = arg.ClientCulture;
            HideezExceptionLocalization.Culture = arg.ClientCulture;

            await RefreshServiceInfo();

            try
            {
                // Client may have only been launched after we already sent an event about workstation unlock
                // This may happen if we are login into the new session where application is not running during unlock but loads afterwards
                // To avoid the confusion, we resend the event about latest unlock method to every client that connects to service
                if (_deviceManager.Devices.Count() == 0)
                {
                    var unlockMethod = await _sessionUnlockMethodMonitor.GetUnlockMethodAsync();
                    await SafePublish(new WorkstationUnlockedMessage(unlockMethod == SessionSwitchSubject.NonHideez));
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }

        }

        async Task OnIntegrationRefreshServiceInfo(Hideez.Integration.Lite.Messages.RefreshServiceInfoMessage arg)
        {
            await RefreshServiceInfo();
        }

        async Task OnRefreshServiceInfo(RefreshServiceInfoMessage arg)
        {
            await RefreshServiceInfo();
        }

        // Engage storage lock upon hub request
        async Task OnLockHwVaultStorage(HesAppConnection_LockHwVaultStorageMessage msg)
        {
            await SafePublish(new LockDeviceStorageMessage(msg.SerialNo));
        }

        // Clear store lock upon hub request
        async Task OnLiftHwVaultStorageLock(HesAppConnection_LiftHwVaultStorageLockMessage msg)
        {
            await SafePublish(new LiftDeviceStorageLockMessage(msg.SerialNo));
        }

        // Clear storage lock when hub connects or disconnects, 
        async Task OnHubConnectionStateChanged(HesAppConnection_HubConnectionStateChangedMessage msg)
        {
            await SafePublish(new LiftDeviceStorageLockMessage(string.Empty));
        }

        // Publish 'WorkstationUnlocked' notification/message when user performs unlock or logon
        private async Task OnSessionSwitch(SessionSwitchMonitor_SessionSwitchMessage msg)
        {
            if (msg.Reason == SessionSwitchReason.SessionUnlock || msg.Reason == SessionSwitchReason.SessionLogon)
            {
                var unlockMethod = await _sessionUnlockMethodMonitor.GetUnlockMethodAsync();
                await SafePublish(new WorkstationUnlockedMessage(unlockMethod == SessionSwitchSubject.NonHideez));
            }
        }

        private async Task LoadUserProximitySettings(LoadUserProximitySettingsMessage msg)
        {
            var settings = _userProximtiySettingsManager.Settings.GetProximitySettings(msg.DeviceConnectionId);
            await SafePublish(new LoadUserProximitySettingsMessageReply(settings));
        }

        private Task SaveUserProximitySettings(SaveUserProximitySettingsMessage msg)
        {
            var settings = _userProximtiySettingsManager.Settings;
            settings.SetProximitySettings(msg.UserDeviceProximitySettings);
            _userProximtiySettingsManager.SaveSettings(settings);

            return Task.CompletedTask;
        }


        private Task RemoveUserProximitySettings(RemoveUserProximitySettingsMessage msg)
        {
            var settings = _userProximtiySettingsManager.Settings;
            settings.RemoveProximitySettings(msg.DeviceConnectionId);
            _userProximtiySettingsManager.SaveSettings(settings);

            return Task.CompletedTask;
        }

        private Task OnClientLanguageSettingsChanged(LanguageSettingsChangedMessage arg)
        {
            TranslationSource.Instance.CurrentCulture = new CultureInfo(arg.NewLanguageName);
            HideezExceptionLocalization.Culture = new CultureInfo(arg.NewLanguageName);

            return Task.CompletedTask;
        }
    }
}
