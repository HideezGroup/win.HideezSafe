using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.CredentialProvider;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.Dongle;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.Localize;
using HideezMiddleware.Tasks;
using HideezMiddleware.Utils.WorkstationHelper;
using Meta.Lib.Modules.PubSub;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinBle;

namespace HideezMiddleware.DeviceConnection.ConnectionProcessors.WinBle
{
    public sealed class AutomaticConnectionProcessor : BaseConnectionProcessor
    {
        readonly WinBleConnectionManager _winBleConnectionManager;
        readonly WinBleConnectionManagerWrapper _winBleConnectionManagerWrapper;
        readonly AdvertisementIgnoreList _advIgnoreListMonitor;
        readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;
        readonly DeviceManager _deviceManager;
        readonly CredentialProviderProxy _credentialProviderProxy;
        readonly IClientUiManager _ui;
        readonly IWorkstationHelper _workstationHelper;
        readonly IMetaPubSub _messenger;
        readonly object _lock = new object();
        int _commandLinkInterlock = 0;

        int _isConnecting = 0;
        bool isRunning = false;

        public AutomaticConnectionProcessor(
            ConnectionFlowProcessorBase connectionFlowProcessor,
            WinBleConnectionManager winBleConnectionManager,
            WinBleConnectionManagerWrapper winBleConnectionManagerWrapper,
            AdvertisementIgnoreList advIgnoreListMonitor,
            IDeviceProximitySettingsProvider proximitySettingsProvider,
            DeviceManager deviceManager,
            CredentialProviderProxy credentialProviderProxy,
            IClientUiManager ui,
            IWorkstationHelper workstationHelper,
            IMetaPubSub messenger,
            ILog log)
            : base(connectionFlowProcessor, SessionSwitchSubject.WinBle, nameof(ProximityConnectionProcessor), messenger, log)
        {
            _winBleConnectionManager = winBleConnectionManager ?? throw new ArgumentNullException(nameof(winBleConnectionManager));
            _winBleConnectionManagerWrapper = winBleConnectionManagerWrapper ?? throw new ArgumentNullException(nameof(winBleConnectionManagerWrapper));
            _advIgnoreListMonitor = advIgnoreListMonitor ?? throw new ArgumentNullException(nameof(advIgnoreListMonitor));
            _proximitySettingsProvider = proximitySettingsProvider ?? throw new ArgumentNullException(nameof(proximitySettingsProvider));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _credentialProviderProxy = credentialProviderProxy ?? throw new ArgumentNullException(nameof(credentialProviderProxy));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _workstationHelper = workstationHelper ?? throw new ArgumentNullException(nameof(workstationHelper));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(_messenger));
        }

        public override void Start()
        {
            lock (_lock)
            {
                if (!isRunning)
                {
                    _winBleConnectionManagerWrapper.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
                    _messenger.Subscribe<ConnectPairedVaultsMessage>(OnConnectPairedVaults);
                    isRunning = true;
                    WriteLine("Started");
                }
            }
        }

        public override void Stop()
        {
            lock (_lock)
            {
                isRunning = false;
                _winBleConnectionManagerWrapper.AdvertismentReceived -= BleConnectionManager_AdvertismentReceived;
                _messenger.Unsubscribe<ConnectPairedVaultsMessage>(OnConnectPairedVaults);
                WriteLine("Stopped");
            }
        }

        async void BleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            await ConnectByProximity(e);
        }

        async Task OnConnectPairedVaults(ConnectPairedVaultsMessage arg)
        {
            WriteLine("Connect paired vaults request");
            _advIgnoreListMonitor.Clear();
            await WaitAdvertisementAndConnectByProximity();
        }

        async Task WaitAdvertisementAndConnectByProximity()
        {
            // Interlock prevents start of multiple or subsequent procedures if impatient user clicks commandLink multiple times
            if (Interlocked.CompareExchange(ref _commandLinkInterlock, 1, 0) == 0)
            {
                var notifId = nameof(AutomaticConnectionProcessor);
                try
                {
                    await _ui.SendError("", notifId);
                    await _ui.SendNotification(TranslationSource.Instance["ConnectionProcessor.SearchingForVault"], notifId);
                    var adv = await new WaitAdvertisementProc(_winBleConnectionManagerWrapper).Run(10_000);
                    if (adv != null)
                    {
                        await ConnectByProximity(adv);
                    }
                    else
                    {
                        await _ui.SendError(TranslationSource.Instance["ConnectionProcessor.VaultNotFound"], notifId);
                    }
                }
                catch (Exception ex)
                {
                    WriteLine(ex.Message);
                }
                finally
                {
                    await _ui.SendNotification("", notifId);
                    Interlocked.Exchange(ref _commandLinkInterlock, 0);
                }
            }
        }

        async Task ConnectByProximity(AdvertismentReceivedEventArgs adv)
        {
            // Standard checks
            if (!isRunning)
                return;

            if (adv == null)
                return;

            if (_isConnecting == 1)
                return;

            // Proximity related checks
            if (_advIgnoreListMonitor.IsIgnored(adv.Id))
                return;

            var connectionId = new ConnectionId(adv.Id, (byte)DefaultConnectionIdProvider.WinBle);
            if (_workstationHelper.IsActiveSessionLocked() && !_proximitySettingsProvider.IsEnabledUnlockByProximity(connectionId))
                return;

            var proximity = BleUtils.RssiToProximity(adv.Rssi);
            if (proximity < _proximitySettingsProvider.GetUnlockProximity(connectionId))
                return;

            if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
            {
                try
                {
                    try
                    {
                        // If device from advertisement already exists and is connected, ignore advertisement
                        var device = _deviceManager.Devices.FirstOrDefault(d =>
                        {
                            return WinBleUtils.WinBleIdToMac(d.DeviceConnection.Connection.ConnectionId.Id)
                                == WinBleUtils.WinBleIdToMac(connectionId.Id)
                                && !(d is IRemoteDeviceProxy);
                        });

                        if (device != null
                            && device.IsConnected
                            && device.GetUserProperty<HwVaultConnectionState>(CustomProperties.HW_CONNECTION_STATE_PROP)
                            == HwVaultConnectionState.Online)
                            return;

                        await ConnectAndUnlockByConnectionId(connectionId);
                    }
                    catch (Exception)
                    {
                        // Silent handling. Log is already printed inside of _connectionFlowProcessor.ConnectAndUnlock()
                        // In case of an error, wait a few seconds, before retrying connection
                        var nextConnectionAttemptDelay = 3_000;
                        await Task.Delay(nextConnectionAttemptDelay);
                    }
                    finally
                    {
                        _advIgnoreListMonitor.Ignore(connectionId.Id);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _isConnecting, 0);
                }
            }
        }
    }
}
