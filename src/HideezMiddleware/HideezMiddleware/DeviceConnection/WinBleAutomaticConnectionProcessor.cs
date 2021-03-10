﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.CredentialProvider;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.Localize;
using HideezMiddleware.Settings;
using HideezMiddleware.Tasks;
using HideezMiddleware.Threading;
using HideezMiddleware.Utils.WorkstationHelper;
using Meta.Lib.Modules.PubSub;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinBle._10._0._18362;

namespace HideezMiddleware.DeviceConnection
{
    public sealed class WinBleAutomaticConnectionProcessor : BaseConnectionProcessor
    {
        readonly WinBleConnectionManager _winBleConnectionManager;
        readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;
        readonly AdvertisementIgnoreList _advIgnoreListMonitor;
        readonly DeviceManager _deviceManager;
        readonly CredentialProviderProxy _credentialProviderProxy;
        readonly IClientUiManager _ui;
        readonly IWorkstationHelper _workstationHelper;
        readonly object _lock = new object();
        int _commandLinkInterlock = 0;

        int _isConnecting = 0;
        bool isRunning = false;

        public WinBleAutomaticConnectionProcessor(
            ConnectionFlowProcessorBase connectionFlowProcessor,
            WinBleConnectionManager winBleConnectionManager,
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
            _proximitySettingsProvider = proximitySettingsProvider ?? throw new ArgumentNullException(nameof(proximitySettingsProvider));
            _advIgnoreListMonitor = advIgnoreListMonitor ?? throw new ArgumentNullException(nameof(advIgnoreListMonitor));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _credentialProviderProxy = credentialProviderProxy ?? throw new ArgumentNullException(nameof(credentialProviderProxy));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _workstationHelper = workstationHelper ?? throw new ArgumentNullException(nameof(workstationHelper));
        }

        public override void Start()
        {
            lock (_lock)
            {
                if (!isRunning)
                {
                    _winBleConnectionManager.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
                    _winBleConnectionManager.ConnectedBondedControllerAdded += BleConnectionManager_ConnectedBondedControllerAdded;
                    _winBleConnectionManager.BondedControllerRemoved += BleConnectionManager_BondedControllerRemoved;
                    _credentialProviderProxy.CommandLinkPressed += CredentialProviderProxy_CommandLinkPressed;
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
                _winBleConnectionManager.AdvertismentReceived -= BleConnectionManager_AdvertismentReceived;
                _winBleConnectionManager.ConnectedBondedControllerAdded -= BleConnectionManager_ConnectedBondedControllerAdded;
                _winBleConnectionManager.BondedControllerRemoved -= BleConnectionManager_BondedControllerRemoved;
                _credentialProviderProxy.CommandLinkPressed -= CredentialProviderProxy_CommandLinkPressed;
                WriteLine("Stopped");
            }
        }

        private async void CredentialProviderProxy_CommandLinkPressed(object sender, EventArgs e)
        {
            // Interlock prevents start of multiple or subsequent procedures if impatient user clicks commandLink multiple times
            if (Interlocked.CompareExchange(ref _commandLinkInterlock, 1, 0) == 0)
            {
                try
                {
                    await _ui.SendError("");
                    await _ui.SendNotification(TranslationSource.Instance["ConnectionProcessor.SearchingForVault"]);
                    var adv = await new WaitAdvertisementProc(_winBleConnectionManager).Run(10_000);
                    if (adv != null)
                    {
                        await ConnectByProximity(adv, true);
                    }
                    else
                    {
                        await _ui.SendNotification("");
                        await _ui.SendError(TranslationSource.Instance["ConnectionProcessor.VaultNotFound"]);
                    }
                }
                catch (Exception ex)
                {
                    WriteLine(ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref _commandLinkInterlock, 0);
                }
            }
        }

        private void BleConnectionManager_BondedControllerRemoved(object sender, ControllerRemovedEventArgs e)
        {
            // Call to IgnoreForTime is added because advertisements may arrive shortly after
            // bond removal is finished, causing an attempt to connect unpaired device which 
            // always results in failure
            _advIgnoreListMonitor.Remove(e.Controller.Id);
            _advIgnoreListMonitor.IgnoreForTime(e.Controller.Id, 2);
        }


        async void BleConnectionManager_ConnectedBondedControllerAdded(object sender, ControllerAddedEventArgs e)
        {
            if (!isRunning)
                return;

            if (e.Controller == null)
                return;

            if (_isConnecting == 1)
                return;

            if (_workstationHelper.IsActiveSessionLocked())
                return;

            if (_advIgnoreListMonitor.IsIgnored(e.Controller.Id))
                return;

            await ConnectById(e.Controller.Id);
        }

        async void BleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            await ConnectByProximity(e);
        }

        async Task ConnectByProximity(AdvertismentReceivedEventArgs adv, bool isCommandLinkPressed = false)
        {
            if (!isRunning)
                return;

            if (adv == null)
                return;

            if (_isConnecting == 1)
                return;

            if (_advIgnoreListMonitor.IsIgnored(adv.Id))
                return;

            var proximity = BleUtils.RssiToProximity(adv.Rssi);

            if (_workstationHelper.IsActiveSessionLocked())
            {
                if(!_proximitySettingsProvider.IsEnabledUnlock(adv.Id))
                    return;

                if (_proximitySettingsProvider.IsDisabledUnlockByProximity(adv.Id) && !isCommandLinkPressed)
                    return;

                if (proximity < _proximitySettingsProvider.GetUnlockProximity(adv.Id))
                {
                    if (isCommandLinkPressed)
                    {
                        await _ui.SendNotification("");
                        await _ui.SendError(TranslationSource.Instance["ConnectionProcessor.LowProximity"]);
                    }

                    return;
                }
            }

            await ConnectById(adv.Id);
        }

        async Task ConnectById(string id)
        {
            // Device must be present in the list of bonded devices to be suitable for connection
            if (_winBleConnectionManager.BondedControllers.FirstOrDefault(c => c.Connection.ConnectionId.Id == id) == null)
                return;

            if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
            {
                try
                {
                    // If device from advertisement already exists and is connected, ignore advertisement
                    var device = _deviceManager.Devices.FirstOrDefault(d => d.DeviceConnection.Connection.ConnectionId.Id == id && !(d is IRemoteDeviceProxy));
                    if (device != null && device.IsConnected)
                        return;

                    try
                    {
                        var connectionId = new ConnectionId(id, (byte)DefaultConnectionIdProvider.WinBle);
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
                        if (!_workstationHelper.IsActiveSessionLocked())
                        {
                            var resultDevice = _deviceManager.Devices.FirstOrDefault(d => d.DeviceConnection.Connection.ConnectionId.Id == id && !(d is IRemoteDeviceProxy));
                            if (resultDevice != null && resultDevice.IsConnected)
                                _advIgnoreListMonitor.Ignore(id);
                            else 
                                _advIgnoreListMonitor.IgnoreForTime(id, 60);
                        }
                        else
                            _advIgnoreListMonitor.Ignore(id);
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
