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
using HideezMiddleware.CredentialProvider;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.Tasks;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;

namespace HideezMiddleware.DeviceConnection.ConnectionProcessors.Other
{
    public sealed class ActivityConnectionProcessor : BaseConnectionProcessor
    {
        readonly IBleConnectionManager _bleConnectionManager;
        readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;
        readonly AdvertisementIgnoreList _advIgnoreListMonitor;
        readonly DeviceManager _deviceManager;
        readonly CredentialProviderProxy _credentialProviderProxy;
        readonly object _lock = new object();

        int _isConnecting = 0;
        bool isRunning = false;

        public ActivityConnectionProcessor(
            ConnectionFlowProcessorBase connectionFlowProcessor,
            IBleConnectionManager bleConnectionManager,
            IDeviceProximitySettingsProvider proximitySettingsProvider,
            AdvertisementIgnoreList advIgnoreListMonitor,
            DeviceManager deviceManager,
            CredentialProviderProxy credentialProviderProxy,
            IMetaPubSub messenger,
            ILog log)
            : base(connectionFlowProcessor, SessionSwitchSubject.Proximity, nameof(ActivityConnectionProcessor), messenger, log)
        {
            _bleConnectionManager = bleConnectionManager ?? throw new ArgumentNullException(nameof(bleConnectionManager));
            _proximitySettingsProvider = proximitySettingsProvider ?? throw new ArgumentNullException(nameof(_proximitySettingsProvider));
            _advIgnoreListMonitor = advIgnoreListMonitor ?? throw new ArgumentNullException(nameof(advIgnoreListMonitor));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _credentialProviderProxy = credentialProviderProxy ?? throw new ArgumentNullException(nameof(credentialProviderProxy));
        }

        public override void Start()
        {
            lock (_lock)
            {
                if (!isRunning)
                {
                    _credentialProviderProxy.ProviderActivated += CredentialProviderProxy_ProviderActivated;
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
                _credentialProviderProxy.ProviderActivated -= CredentialProviderProxy_ProviderActivated;
                WriteLine("Stopped");
            }
        }

        private async void CredentialProviderProxy_ProviderActivated(object sender, EventArgs e)
        {
            WriteLine("Activity detected");
            _advIgnoreListMonitor.Clear();
            await WaitAdvertisementAndConnectByActivity();
        }

        async Task WaitAdvertisementAndConnectByActivity()
        {
            try
            {
                var adv = await new WaitAdvertisementProc(_bleConnectionManager).Run(10_000);
                if (adv == null)
                    return;

                await ConnectByActivity(adv);
            }
            catch (Exception ex)
            {
                WriteLine(ex.Message);
            }
        }

        async Task ConnectByActivity(AdvertismentReceivedEventArgs adv)
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

            var connectionId = new ConnectionId(adv.Id, _bleConnectionManager.Id);
            if (!_proximitySettingsProvider.IsEnabledUnlockByActivity(connectionId))
                return;

            var proximity = BleUtils.RssiToProximity(adv.Rssi);
            if (proximity < _proximitySettingsProvider.GetUnlockProximity(connectionId))
                return;

            if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
            {
                try
                {
                    var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == adv.Id && !(d is IRemoteDeviceProxy) && !d.IsBoot);

                    // Unlocked Workstation, Device not found OR Device not connected - dont add to ignore
                    if (!_credentialProviderProxy.IsConnected && (device == null || (device != null && !device.IsConnected)))
                        return;

                    try
                    {
                        // Unlocked Workstation, Device connected - add to ignore
                        if (!_credentialProviderProxy.IsConnected && device != null && device.IsConnected)
                            return;

                        // Locked Workstation, Device not found OR not connected - connect add to ignore
                        if (_credentialProviderProxy.IsConnected && (device == null || (device != null && !device.IsConnected)))
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
