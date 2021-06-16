using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.DeviceConnection.ConnectionProcessors.Dongle
{

    public sealed class ProximityConnectionProcessor : BaseConnectionProcessor
    {
        readonly IBleConnectionManager _bleConnectionManager;
        readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;
        readonly AdvertisementIgnoreList _advIgnoreListMonitor;
        readonly DeviceManager _deviceManager;
        readonly IWorkstationUnlocker _workstationUnlocker;
        readonly object _lock = new object();

        int _isConnecting = 0;
        bool isRunning = false;

        public ProximityConnectionProcessor(
            IConnectionFlowProcessor connectionFlowProcessor,
            IBleConnectionManager bleConnectionManager,
            IDeviceProximitySettingsProvider proximitySettingsProvider,
            AdvertisementIgnoreList advIgnoreListMonitor,
            DeviceManager deviceManager,
            IWorkstationUnlocker workstationUnlocker,
            IMetaPubSub messenger,
            ILog log) 
            : base(connectionFlowProcessor, SessionSwitchSubject.Proximity, nameof(ProximityConnectionProcessor), messenger, log)
        {
            _bleConnectionManager = bleConnectionManager ?? throw new ArgumentNullException(nameof(bleConnectionManager));
            _proximitySettingsProvider = proximitySettingsProvider ?? throw new ArgumentNullException(nameof(_proximitySettingsProvider));
            _advIgnoreListMonitor = advIgnoreListMonitor ?? throw new ArgumentNullException(nameof(advIgnoreListMonitor));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _workstationUnlocker = workstationUnlocker ?? throw new ArgumentNullException(nameof(workstationUnlocker));
        }

        public override void Start()
        {
            lock (_lock)
            {
                if (!isRunning)
                {
                    _bleConnectionManager.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
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
                WriteLine("Stopped");
            }
        }

        async void BleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            await ConnectByProximity(e);
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

            var connectionId = new ConnectionId(adv.Id, (byte)DefaultConnectionIdProvider.Csr);
            if (!_proximitySettingsProvider.IsEnabledUnlockByProximity(connectionId))
                return;

            var proximity = BleUtils.RssiToProximity(adv.Rssi);
            if (proximity < _proximitySettingsProvider.GetUnlockProximity(connectionId))
                return;

            if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
            {
                try
                {
                    var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == adv.Id && !(d is IRemoteDeviceProxy) && !d.IsBoot);

                    // Unlocked Workstation and device haven't finished main workflow - break and dont add to ignore
                    if (!_workstationUnlocker.IsConnected 
                        && device?.GetUserProperty<bool>(DeviceCustomProperties.HV_FINISHED_WF) != true)
                        return;

                    try
                    {
                        // Unlocked Workstation and device finished main workflow - break and add to ignore
                        if (!_workstationUnlocker.IsConnected 
                            && device?.GetUserProperty<bool>(DeviceCustomProperties.HV_FINISHED_WF) == true)
                            return;

                        // Locked Workstation and haven't finished main workflow - connect add to ignore
                        if (_workstationUnlocker.IsConnected 
                            && device?.GetUserProperty<bool>(DeviceCustomProperties.HV_FINISHED_WF) != true)
                        {
                            await ConnectAndUnlockByConnectionId(connectionId);
                        }
                    }
                    catch (Exception)
                    {
                        // Silent handling. Log is already printed inside of _connectionFlowProcessor.ConnectAndUnlock()
                    }
                    finally
                    {
                        _advIgnoreListMonitor.Ignore(adv.Id);
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
