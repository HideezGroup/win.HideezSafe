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
using HideezMiddleware.DeviceConnection.Workflow;
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
        readonly IBleConnectionManager _bleConnectionManager;
        readonly AdvertisementIgnoreList _advIgnoreListMonitor;
        readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;
        readonly DeviceManager _deviceManager;
        readonly IWorkstationUnlocker _workstationUnlocker;
        readonly IClientUiManager _ui;
        readonly IWorkstationHelper _workstationHelper;
        readonly IMetaPubSub _messenger;
        readonly object _lock = new object();
        int _commandLinkInterlock = 0;

        int _isConnecting = 0;
        bool isRunning = false;

        public AutomaticConnectionProcessor(
            IConnectionFlowProcessor connectionFlowProcessor,
            IBleConnectionManager bleConnectionManager,
            AdvertisementIgnoreList advIgnoreListMonitor,
            IDeviceProximitySettingsProvider proximitySettingsProvider,
            DeviceManager deviceManager,
            IWorkstationUnlocker workstationUnlocker,
            IClientUiManager ui,
            IWorkstationHelper workstationHelper,
            IMetaPubSub messenger,
            ILog log)
            : base(connectionFlowProcessor, SessionSwitchSubject.WinBle, nameof(ProximityConnectionProcessor), messenger, log)
        {
            _bleConnectionManager = bleConnectionManager ?? throw new ArgumentNullException(nameof(bleConnectionManager));
            _advIgnoreListMonitor = advIgnoreListMonitor ?? throw new ArgumentNullException(nameof(advIgnoreListMonitor));
            _proximitySettingsProvider = proximitySettingsProvider ?? throw new ArgumentNullException(nameof(proximitySettingsProvider));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _workstationUnlocker = workstationUnlocker ?? throw new ArgumentNullException(nameof(workstationUnlocker));
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
                    _bleConnectionManager.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
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
                _bleConnectionManager.AdvertismentReceived -= BleConnectionManager_AdvertismentReceived;
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
                    var adv = await new WaitAdvertisementProc(_bleConnectionManager).Run(10_000);
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
            var connectionId = new ConnectionId(adv.Id, (byte)DefaultConnectionIdProvider.WinBle);
            if (_workstationHelper.IsActiveSessionLocked() && !_proximitySettingsProvider.IsEnabledUnlockByProximity(connectionId))
                return;

            var proximity = BleUtils.RssiToProximity(adv.Rssi);
            if (proximity < _proximitySettingsProvider.GetUnlockProximity(connectionId))
                return;

            // Checks below are in place to fix ghosted devices that are connected by cannot
            // start workflow because their advertisements are ignored by advIgnoreList
            // Checking that ignored advertisements are from device that is not connected solves this issue

            var device = _deviceManager.Devices.FirstOrDefault(d =>
            {
                return WinBleUtils.WinBleIdToMac(d.DeviceConnection.Connection.ConnectionId.Id)
                    == WinBleUtils.WinBleIdToMac(connectionId.Id)
                    && !(d is IRemoteDeviceProxy)
                    && !d.IsBoot;
            });

            // Only break for ignored devices that are also not connected or don't exist devices
            if (device?.IsConnected != true && _advIgnoreListMonitor.IsIgnored(adv.Id))
                return;

            if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
            {
                try
                {
                    try
                    {
                        // If device from advertisement already exists and is connected, ignore advertisement
                        if (device?.GetUserProperty<bool>(WorkflowProperties.HV_FINISHED_WF) == true)
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
