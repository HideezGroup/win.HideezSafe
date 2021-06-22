using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinBle;

namespace HideezMiddleware
{
    /// <summary>
    /// Temporary wrapper for WinBleConnectionManager that iminates physical disconnection of controllers and
    /// acts as adapter between service logic and implementation restrictions of WinBle support
    /// </summary>
    public sealed class WinBleConnectionManagerWrapper : Logger, IBleConnectionManager
    {
        readonly WinBleConnectionManager _winBleConnectionManager;
        readonly ConcurrentDictionary<string, IConnectionController> _connectedControllers = new ConcurrentDictionary<string, IConnectionController>();
        
        public BluetoothAdapterState State => _winBleConnectionManager.State;

        public bool SupportsDiscoveryAndDeviceManagement => _winBleConnectionManager.SupportsDiscoveryAndDeviceManagement;

        public byte Id => _winBleConnectionManager.Id;

        public IReadOnlyCollection<IConnectionController> ConnectionControllers => _connectedControllers.Values.ToList().AsReadOnly();

        public event EventHandler<AdvertismentReceivedEventArgs> AdvertismentReceived;
        public event EventHandler<DiscoveredDeviceAddedEventArgs> DiscoveredDeviceAdded;
        public event EventHandler<DiscoveredDeviceRemovedEventArgs> DiscoveredDeviceRemoved;
        public event EventHandler AdapterStateChanged;
        public event EventHandler<ControllerAddedEventArgs> ControllerAdded;
        public event EventHandler<ControllerRemovedEventArgs> ControllerRemoved;

        public WinBleConnectionManagerWrapper(WinBleConnectionManager winBleConnectionManager, ILog log)
            : base(nameof(WinBleConnectionManagerWrapper), log)
        {
            _winBleConnectionManager = winBleConnectionManager;

            _winBleConnectionManager.AdvertismentReceived += WinBleConnectionManager_AdvertismentReceived;
            _winBleConnectionManager.DiscoveredDeviceAdded += WinBleConnectionManager_DiscoveredDeviceAdded;
            _winBleConnectionManager.DiscoveredDeviceRemoved += WinBleConnectionManager_DiscoveredDeviceRemoved;
            _winBleConnectionManager.AdapterStateChanged += WinBleConnectionManager_AdapterStateChanged;
            _winBleConnectionManager.ControllerAdded += WinBleConnectionManager_ControllerAdded;
            _winBleConnectionManager.ControllerRemoved += WinBleConnectionManager_ControllerRemoved;
        }

        private void WinBleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            SafeInvoke(AdvertismentReceived, e);
        }

        private void WinBleConnectionManager_DiscoveredDeviceAdded(object sender, DiscoveredDeviceAddedEventArgs e)
        {
            SafeInvoke(DiscoveredDeviceAdded, e);
        }

        private void WinBleConnectionManager_DiscoveredDeviceRemoved(object sender, DiscoveredDeviceRemovedEventArgs e)
        {
            SafeInvoke(DiscoveredDeviceRemoved, e);
        }

        private void WinBleConnectionManager_AdapterStateChanged(object sender, EventArgs e)
        {
            try
            {
                AdapterStateChanged?.Invoke(this, e);
            }
            catch (Exception)
            {
            }
        }

        private void WinBleConnectionManager_ControllerAdded(object sender, ControllerAddedEventArgs e)
        {
        }

        private void WinBleConnectionManager_ControllerRemoved(object sender, ControllerRemovedEventArgs e)
        {
            _connectedControllers.TryRemove(e.Controller.Id, out IConnectionController controller);

            SafeInvoke(ControllerRemoved, e);
        }

        void TryAddConnectionController(IConnectionController controller)
        {
            if (controller != null)
            {
                if (_connectedControllers.TryAdd(controller.Id, controller))
                {
                    SafeInvoke(ControllerAdded, new ControllerAddedEventArgs(controller));
                }
            }
        }

        void TryRemoveConnectionController(IConnectionController controller)
        {
            if (controller != null)
            {
                if (_connectedControllers.TryRemove(controller.Id, out IConnectionController removedController))
                {
                    SafeInvoke(ControllerRemoved, new ControllerRemovedEventArgs(removedController));
                }
            }
        }

        public async Task Restart()
        {
            await _winBleConnectionManager.Restart();
        }

        public async Task Start()
        {
            await _winBleConnectionManager.Start();
        }

        public async Task Stop()
        {
            await _winBleConnectionManager.Stop();
        }

        public async Task<IConnectionController> Connect(ConnectionId connectionId)
        {
            var controller = await _winBleConnectionManager.Connect(connectionId);
            TryAddConnectionController(controller);
            return controller;
        }

        public async Task<IConnectionController> PairAndConnect(ConnectionId id)
        {
            var controller = await _winBleConnectionManager.PairAndConnect(id);
            TryAddConnectionController(controller);
            return controller;
        }

        public Task Disconnect(IConnectionController controller)
        {
            TryRemoveConnectionController(controller);
            return Task.CompletedTask;
        }

        public Task RemoveConnection(IConnectionController controller)
        {
            TryRemoveConnectionController(controller);
            return Task.CompletedTask;
        }

        public async Task DeleteBond(IConnectionController controller)
        {
            await _winBleConnectionManager.DeleteBond(controller);
        }

        public async Task DeleteBond(ConnectionId connectionId)
        {
            await _winBleConnectionManager.DeleteBond(connectionId);
        }

    }
}
