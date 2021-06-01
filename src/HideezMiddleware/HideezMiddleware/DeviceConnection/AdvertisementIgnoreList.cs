using HideezMiddleware.Settings;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using Hideez.SDK.Communication.Proximity.Interfaces;
using Hideez.SDK.Communication.Connection;

namespace HideezMiddleware.DeviceConnection
{
    public class AdvertisementIgnoreList : Logger, IDisposable
    {
        internal class IgnoreEntry
        {
            public DateTime Added { get; } = DateTime.UtcNow;
            public DateTime LastReceivedTime { get; set; } = DateTime.UtcNow;
            public int Lifetime { get; }

            public IgnoreEntry(int lifetime = 0)
            {
                Lifetime = lifetime;
            }
        }

        readonly IBleConnectionManager _bleConnectionManager;
        readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;

        readonly Dictionary<string, IgnoreEntry> _ignoreDict = new Dictionary<string, IgnoreEntry>();
        readonly object _lock = new object();
        readonly int _rssiClearDelaySeconds;

        public AdvertisementIgnoreList(
            IBleConnectionManager bleConnectionManager,
            IDeviceProximitySettingsProvider proximitySettingsProvider,
            int rssiClearDelaySeconds,
            ILog log)
            : base(nameof(AdvertisementIgnoreList), log)
        {
            _bleConnectionManager = bleConnectionManager;
            _proximitySettingsProvider = proximitySettingsProvider;
            _rssiClearDelaySeconds = rssiClearDelaySeconds;

            _bleConnectionManager.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
            _bleConnectionManager.AdapterStateChanged += BleConnectionManager_AdapterStateChanged;
            _bleConnectionManager.ControllerAdded += BleConnectionManager_ControllerAdded;
            _bleConnectionManager.ControllerRemoved += BleConnectionManager_ControllerRemoved;
        }


        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                _bleConnectionManager.AdvertismentReceived -= BleConnectionManager_AdvertismentReceived;
                _bleConnectionManager.AdapterStateChanged -= BleConnectionManager_AdapterStateChanged;
                _bleConnectionManager.ControllerAdded -= BleConnectionManager_ControllerAdded;
                _bleConnectionManager.ControllerRemoved -= BleConnectionManager_ControllerRemoved;
                Clear();
            }

            disposed = true;
        }

        ~AdvertisementIgnoreList()
        {
            Dispose(false);
        }
        #endregion

        /// <summary>
        /// The method of adding the device Id to the ignore list or extending it in the ignore list
        /// for seconds of delay defined in the constructor
        /// </summary>
        /// <param name="id">The device Id</param>
        public void Ignore(string id)
        {
            lock (_lock)
            {
                if (!_ignoreDict.ContainsKey(id))
                {
                    _ignoreDict[id] = new IgnoreEntry();
                    WriteLine($"Added new item to advertisment ignore list: {id}, indefinite");
                }

                _ignoreDict[id].LastReceivedTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// The method of adding the connection Id to the ignore list for defined time.
        /// </summary>
        /// <param name="id">The connection Id.</param>
        /// <param name="lifetimeSeconds">The time, in seconds, in which connection Id will be removed from the ignore list.</param>
        public void IgnoreForTime(string id, int lifetimeSeconds)
        {
            lock (_lock)
            {
                if (!_ignoreDict.ContainsKey(id))
                {
                    _ignoreDict[id] = new IgnoreEntry(lifetimeSeconds);
                    WriteLine($"Added new item to advertisment ignore list: {id}, lifetime: {lifetimeSeconds}s");
                }

                _ignoreDict[id].LastReceivedTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// The method of checking that the ignore list contains the connection Id.
        /// </summary>
        /// <param name="id">The connection Id.</param>
        /// <returns>True, if the ignore list contains the connection Id. False, if doesn't.</returns>
        public bool IsIgnored(string id)
        {
            lock (_lock)
            {
                RemoveTimedOutRecords();

                return _ignoreDict.ContainsKey(id);
            }
        }

        /// <summary>
        /// Removes all items from the ignore list.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                WriteLine("Clearing advertisment ignore list");
                _ignoreDict.Clear();
            }
        }

        void BleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            lock (_lock)
            {
                RemoveTimedOutRecords();

                if (_ignoreDict.ContainsKey(e.Id))
                {
                    var proximity = BleUtils.RssiToProximity(e.Rssi);

                    var connectionId = new ConnectionId(e.Id, _bleConnectionManager.Id);
                    if (proximity > _proximitySettingsProvider.GetLockProximity(connectionId))
                        _ignoreDict[e.Id].LastReceivedTime = DateTime.UtcNow;
                }
            }
        }

        void BleConnectionManager_AdapterStateChanged(object sender, EventArgs e)
        {
            // TODO: When "Resetting" state is implemented, instead clear list on all changes except "PoweredOn" and "Resetting"
            if (_bleConnectionManager.State == BluetoothAdapterState.PoweredOff || _bleConnectionManager.State == BluetoothAdapterState.Unknown)
                Clear();
        }

        void BleConnectionManager_ControllerAdded(object sender, ControllerAddedEventArgs e)
        {
            e.Controller.ConnectionStateChanged += Controller_ConnectionStateChanged;
        }
        
        void BleConnectionManager_ControllerRemoved(object sender, ControllerRemovedEventArgs e)
        {
            e.Controller.ConnectionStateChanged -= Controller_ConnectionStateChanged;
        }

        void Controller_ConnectionStateChanged(object sender, EventArgs e)
        {
            var controller = sender as IConnectionController;
            if (controller?.State == Hideez.SDK.Communication.ConnectionState.NotConnected)
            {
                Ignore(controller.Id);
            }
            else
            {
                Remove(controller.Id);
            }
        }

        void RemoveTimedOutRecords()
        {
            lock (_lock)
            {
                if (_ignoreDict.Count > 0)
                {
                    var now = DateTime.UtcNow;

                    // Remove Id's from ignore list if we did not receive an advertisement from them in _rssiClearDelaySeconds seconds
                    var itemsToRemove = _ignoreDict.Where(item => (now - item.Value.LastReceivedTime).TotalSeconds >= _rssiClearDelaySeconds).ToList();
                    foreach(var item in itemsToRemove)
                        Remove(item.Key);

                    // Remote Id's from list with expended lifetime that is greater than 0
                    itemsToRemove = _ignoreDict.Where(item => item.Value.Lifetime > 0 && (now - item.Value.Added).TotalSeconds >= item.Value.Lifetime).ToList();
                    foreach (var item in itemsToRemove)
                        Remove(item.Key);
                }
            }
        }

        /// <summary>
        /// The method to remove a specific connection Id from the ignore list.
        /// </summary>
        /// <param name="id">The connection Id.</param>
        public void Remove(string id)
        {
            lock (_lock)
            {
                if (_ignoreDict.Remove(id))
                    WriteLine($"Removed item from advertisment ignore list: {id}");
            }
        }
    }
}
