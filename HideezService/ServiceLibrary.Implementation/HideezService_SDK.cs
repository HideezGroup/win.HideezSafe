﻿using Hideez.CsrBLE;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity;
using HideezMiddleware;
using System;
using System.Threading.Tasks;

namespace ServiceLibrary.Implementation
{
    public partial class HideezService : IHideezService, IWorkstationLocker
    {
        static EventLogger _log;
        static BleConnectionManager _connectionManager;
        static BleDeviceManager _deviceManager;
        static CredentialProviderConnection _credentialProviderConnection;
        static WorkstationUnlocker _workstationUnlocker;
        static HesAppConnection _hesConnection;
        static RfidServiceConnection _rfidService;
        static ProximityMonitorManager _proximityMonitorManager;

        private void InitializeSDK()
        {
            _log = new EventLogger("ExampleApp");
            _connectionManager = new BleConnectionManager(_log, "d:\\temp\\bonds"); //todo

            _connectionManager.AdapterStateChanged += ConnectionManager_AdapterStateChanged;
            _connectionManager.DiscoveryStopped += ConnectionManager_DiscoveryStopped;
            _connectionManager.DiscoveredDeviceAdded += ConnectionManager_DiscoveredDeviceAdded;
            _connectionManager.DiscoveredDeviceRemoved += ConnectionManager_DiscoveredDeviceRemoved;

            // COM =============================
            //var port = new ComConnection(log, "COM68", 9600);
            //port.Connect();

            // BLE ============================
            _deviceManager = new BleDeviceManager(_log, _connectionManager);
            _deviceManager.DeviceAdded += DevicesManager_DeviceCollectionChanged;
            _deviceManager.DeviceRemoved += DevicesManager_DeviceCollectionChanged;


            // Named Pipes Server ==============================
            _credentialProviderConnection = new CredentialProviderConnection(_log);
            _credentialProviderConnection.Start();


            // RFID Service Connection ============================
            _rfidService = new RfidServiceConnection(_log);
            _rfidService.RfidAdapterStateChanged += RFIDService_ReaderStateChanged;
            _rfidService.Start();

            // WorkstationUnlocker ==================================
            //_workstationUnlocker = new WorkstationUnlocker(_deviceManager, _credentialProviderConnection, _rfidService, _log);


            // HES
            _hesConnection = new HesAppConnection(_deviceManager, "http://192.168.10.241", _log);
            _hesConnection.HubConnectionStateChanged += HES_ConnectionStateChanged;
            _hesConnection.Connect();

            // Proximity Monitor
            _proximityMonitorManager = new ProximityMonitorManager(_deviceManager, this);
            _proximityMonitorManager.Start();

            _connectionManager.Start();
            //_connectionManager.StartDiscovery();
        }

        void ConnectionManager_AdapterStateChanged(object sender, EventArgs e)
        {
            foreach (var client in SessionManager.Sessions)
                client.Callbacks.ConnectionDongleChangedRequest(_connectionManager?.State == BluetoothAdapterState.PoweredOn);
        }

        void RFIDService_ReaderStateChanged(object sender, EventArgs e)
        {
            foreach (var client in SessionManager.Sessions)
                client.Callbacks.ConnectionRFIDChangedRequest(_rfidService != null ? _rfidService.Connected : false);
        }

        void HES_ConnectionStateChanged(object sender, EventArgs e)
        {
            foreach (var client in SessionManager.Sessions)
                client.Callbacks.ConnectionHESChangedRequest(_hesConnection?.State == HesConnectionState.Connected);
        }

        void DevicesManager_DeviceCollectionChanged(object sender, DeviceCollectionChangedEventArgs e)
        {
        }

        void ConnectionManager_DiscoveredDeviceAdded(object sender, DiscoveredDeviceAddedEventArgs e)
        {
        }

        void ConnectionManager_DiscoveredDeviceRemoved(object sender, DiscoveredDeviceRemovedEventArgs e)
        {
        }

        void ConnectionManager_DiscoveryStopped(object sender, EventArgs e)
        {
        }

        public bool GetAdapterState(Addapter addapter)
        {
            switch (addapter)
            {
                case Addapter.Dongle:
                    return _connectionManager?.State == BluetoothAdapterState.PoweredOn;
                case Addapter.HES:
                    return _hesConnection?.State == HesConnectionState.Connected;
                case Addapter.RFID:
                    return _rfidService != null ? _rfidService.Connected : false;
                default:
                    return false;
            }
        }

        public void LockWorkstation()
        {
            foreach (var client in SessionManager.Sessions)
                client.Callbacks.LockWorkstationRequest();
        }
    }
}
