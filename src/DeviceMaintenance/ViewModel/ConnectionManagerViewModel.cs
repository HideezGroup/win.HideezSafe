using DeviceMaintenance.Messages;
using Hideez.CsrBLE;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.FW;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Utils;
using MvvmExtensions.PropertyChangedMonitoring;
using System;
using System.IO;
using System.Threading.Tasks;
using WinBle;

namespace DeviceMaintenance.ViewModel
{
    public class ConnectionManagerViewModel : PropertyChangedImplementation
    {
        readonly ConnectionManagersCoordinator _connectionManagersCoordinator;
        readonly WinBleConnectionManager _winBleConnectionManager;
        readonly BleConnectionManager _csrConnectionManager;
        readonly DeviceManager _deviceManager;
        readonly EventLogger _log;
        readonly MetaPubSub _hub;
        IBleConnectionManager _activeConnectionManager;

        public bool BleAdapterAvailable => _activeConnectionManager?.State == BluetoothAdapterState.PoweredOn;

        public ConnectionManagerViewModel(EventLogger log, MetaPubSub hub)
        {
            _log = log;
            _hub = hub;
            _hub.Subscribe<ConnectDeviceCommand>(OnConnectDeviceCommand);
            _hub.Subscribe<EnterBootCommand>(OnEnterBootCommand);
            _hub.Subscribe<DeviceWipedEvent>(OnDeviceWipedEvent);
            _hub.Subscribe<ClosingEvent>(OnClosing);

            var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var bondsFolderPath = $"{commonAppData}\\Hideez\\Service\\Bonds";

            // ConnectionManager ============================
            Directory.CreateDirectory(bondsFolderPath); // Ensure directory for bonds is created since unmanaged code doesn't do that

            _csrConnectionManager = new BleConnectionManager(log, bondsFolderPath);
            _csrConnectionManager.AdapterStateChanged += ConnectionManager_AdapterStateChanged;
            _csrConnectionManager.AdvertismentReceived += ConnectionManager_AdvertismentReceived;
            _winBleConnectionManager = new WinBleConnectionManager(log);
            _winBleConnectionManager.AdapterStateChanged += ConnectionManager_AdapterStateChanged;
            _winBleConnectionManager.ControllerAdded += WinBleConnectionManager_ControllerAdded;

            // Connection Managers Coordinator ============================
            _connectionManagersCoordinator = new ConnectionManagersCoordinator();
            _connectionManagersCoordinator.AddConnectionManager(_winBleConnectionManager);
            _connectionManagersCoordinator.AddConnectionManager(_csrConnectionManager);
            //_connectionManagersCoordinator.Start();

            // DeviceManager ============================
            _deviceManager = new DeviceManager(_connectionManagersCoordinator, log);
        }

        Task OnClosing(ClosingEvent arg)
        {
            _winBleConnectionManager.Dispose();
            return Task.CompletedTask;
        }

        public void Initialize(DefaultConnectionIdProvider connectionType)
        {
            //_activeConnectionManager?.Restart();
            _connectionManagersCoordinator.Stop();

            if (connectionType == DefaultConnectionIdProvider.Csr)
            {
                _activeConnectionManager = _csrConnectionManager;
            }
            else if (connectionType == DefaultConnectionIdProvider.WinBle)
            {
                _activeConnectionManager = _winBleConnectionManager;
            }

            _activeConnectionManager.Start();

            NotifyPropertyChanged(nameof(BleAdapterAvailable));
        }

        void ConnectionManager_AdapterStateChanged(object sender, EventArgs e)
        {
            NotifyPropertyChanged(nameof(BleAdapterAvailable));
        }

        void ConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            if (e.Rssi > SdkConfig.TapProximityUnlockThreshold)
                _hub.Publish(new AdvertismentReceivedEvent(e));
        }

        void WinBleConnectionManager_ControllerAdded(object sender, ControllerAddedEventArgs e)
        {
            _hub.Publish(new ControllerAddedEvent(e.Controller.Connection.ConnectionId));
        }

        async Task OnConnectDeviceCommand(ConnectDeviceCommand arg)
        {
            IDevice device = null;
            try
            {
                device = await _deviceManager.Connect(arg.ConnectionId).TimeoutAfter(SdkConfig.ConnectDeviceTimeout);
            }
            catch (TimeoutException) { }

            try
            {
                if (device == null)
                    device = await _deviceManager.Connect(arg.ConnectionId).TimeoutAfter(SdkConfig.ConnectDeviceTimeout / 2);
            }
            catch (TimeoutException) { }

            if (device == null)
            {
                await _deviceManager.DeleteBond(arg.ConnectionId);
                
                if(_activeConnectionManager.Id == (byte)DefaultConnectionIdProvider.Csr)
                    device = await _deviceManager.Connect(arg.ConnectionId).TimeoutAfter(SdkConfig.ConnectDeviceTimeout);
            }

            if (device != null)
                await device.WaitInitialization(SdkConfig.DeviceInitializationTimeout, default);

            await _hub.Publish(new ConnectDeviceResponse(device, arg.ConnectionId));
        }

        async Task OnEnterBootCommand(EnterBootCommand cmd)
        {
            var imageUploader = new FirmwareImageUploader(
                cmd.FirmwareFilePath, _deviceManager, cmd.Device as Device, cmd.LongOperation, _log);
            await imageUploader.EnterBoot();
            await _hub.Publish(new EnterBootResponse(imageUploader));
        }

        async Task OnDeviceWipedEvent(DeviceWipedEvent arg)
        {
            await _deviceManager.RemoveConnection(arg.DeviceViewModel.Device.DeviceConnection);
        }

        public bool IsBonded(string id)
        {
            if(_activeConnectionManager.Id == (byte)DefaultConnectionIdProvider.Csr)
                return _csrConnectionManager.IsBonded(id);
            return true;
        }
    }
}
