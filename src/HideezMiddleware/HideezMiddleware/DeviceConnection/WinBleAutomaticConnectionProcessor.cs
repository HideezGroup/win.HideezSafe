﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Utils;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.Settings;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinBle._10._0._18362;

namespace HideezMiddleware.DeviceConnection
{
    public sealed class WinBleAutomaticConnectionProcessor : BaseConnectionProcessor, IDisposable
    {
        readonly WinBleConnectionManager _winBleConnectionManager;
        readonly ISettingsManager<WorkstationSettings> _workstationSettingsManager;
        readonly AdvertisementIgnoreList _advIgnoreListMonitor;
        readonly DeviceManager _deviceManager;
        readonly object _lock = new object();

        int _isConnecting = 0;
        bool isRunning = false;

        public WinBleAutomaticConnectionProcessor(
            ConnectionFlowProcessor connectionFlowProcessor,
            WinBleConnectionManager winBleConnectionManager,
            AdvertisementIgnoreList advIgnoreListMonitor,
            ISettingsManager<WorkstationSettings> workstationSettingsManager,
            DeviceManager deviceManager,
            ILog log)
            : base(connectionFlowProcessor, nameof(ProximityConnectionProcessor), log)
        {
            _winBleConnectionManager = winBleConnectionManager ?? throw new ArgumentNullException(nameof(winBleConnectionManager));
            _workstationSettingsManager = workstationSettingsManager ?? throw new ArgumentNullException(nameof(workstationSettingsManager));
            _advIgnoreListMonitor = advIgnoreListMonitor ?? throw new ArgumentNullException(nameof(advIgnoreListMonitor));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        }

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed = false;
        void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                _winBleConnectionManager.AdvertismentReceived -= BleConnectionManager_AdvertismentReceived;
            }

            disposed = true;
        }

        ~WinBleAutomaticConnectionProcessor()
        {
            Dispose(false);
        }
        #endregion

        public override void Start()
        {
            lock (_lock)
            {
                if (!isRunning)
                {
                    _winBleConnectionManager.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
                    _winBleConnectionManager.ConnectedBondedControllerAdded += BleConnectionManager_ConnectedBondedControllerAdded;
                    _winBleConnectionManager.BondedControllerRemoved += BleConnectionManager_BondedControllerRemoved;
                    isRunning = true;
                    WriteLine("Started");
                }
            }
        }

        private void BleConnectionManager_BondedControllerRemoved(object sender, ControllerRemovedEventArgs e)
        {
            _advIgnoreListMonitor.Remove(e.Controller.Id);
        }

        public override void Stop()
        {
            lock (_lock)
            {
                isRunning = false;
                _winBleConnectionManager.AdvertismentReceived -= BleConnectionManager_AdvertismentReceived;
                _winBleConnectionManager.ConnectedBondedControllerAdded -= BleConnectionManager_ConnectedBondedControllerAdded;
                _winBleConnectionManager.BondedControllerRemoved -= BleConnectionManager_BondedControllerRemoved;
                WriteLine("Stopped");
            }
        }

        async void BleConnectionManager_ConnectedBondedControllerAdded(object sender, ControllerAddedEventArgs e)
        {
            if (!isRunning)
                return;

            if (e.Controller == null)
                return;

            if (_isConnecting == 1)
                return;

            if (WorkstationHelper.IsActiveSessionLocked())
                return;

            await ConnectById(e.Controller.Id);
        }


        async void BleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            await ConnectByProximity(e);
        }

        async Task ConnectByProximity(AdvertismentReceivedEventArgs adv)
        {
            if (!isRunning)
                return;

            if (adv == null)
                return;

            if (_isConnecting == 1)
                return;

            var proximity = BleUtils.RssiToProximity(adv.Rssi);
            var settings = _workstationSettingsManager.Settings;

            if(WorkstationHelper.IsActiveSessionLocked())
                if (proximity < settings.UnlockProximity)
                    return;
            else
                if (proximity < settings.LockProximity)
                    return;

            await ConnectById(adv.Id);
        }

        async Task ConnectById(string id)
        {
            if (_advIgnoreListMonitor.IsIgnored(id))
                return;

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
