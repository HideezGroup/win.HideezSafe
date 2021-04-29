using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.ReconnectManager;
using System;
using System.Linq;

namespace HideezMiddleware
{
    public class WorkstationLockProcessor : Logger, IDisposable
    {
        public const string PROX_LOCK_ENABLED_PROP = "ProximityLockEnabled";

        readonly ConnectionFlowProcessorBase _flowProcessor;
        readonly ProximityMonitorManager _proximityMonitorManager;
        readonly DeviceManager _deviceManager;
        readonly IWorkstationLocker _workstationLocker;
        readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;
        readonly DeviceReconnectManager _deviceReconnectManager;

        readonly object _deviceListsLock = new object();

        public event EventHandler<IDevice> DeviceProxLockEnabled;
        public event EventHandler<WorkstationLockingEventArgs> WorkstationLocking;

        public WorkstationLockProcessor(
            ConnectionFlowProcessorBase flowProcessor, 
            ProximityMonitorManager proximityMonitorManager, 
            DeviceManager deviceManager, 
            IWorkstationLocker workstationLocker,
            IDeviceProximitySettingsProvider proximitySettingsProvider,
            DeviceReconnectManager deviceReconnectManager,
            ILog log)
            :base(nameof(WorkstationLockProcessor), log)
        {
            _flowProcessor = flowProcessor;
            _proximityMonitorManager = proximityMonitorManager;
            _deviceManager = deviceManager;
            _workstationLocker = workstationLocker;
            _proximitySettingsProvider = proximitySettingsProvider;
            _deviceReconnectManager = deviceReconnectManager;

            _flowProcessor.DeviceFinilizingMainFlow += FlowProcessor_DeviceFinalizingMainFlow;

            _proximityMonitorManager.DeviceBelowLockForToLong += ProximityMonitorManager_DeviceBelowLockForToLong;
            _proximityMonitorManager.DeviceProximityTimeout += ProximityMonitorManager_DeviceProximityTimeout;
            _proximityMonitorManager.DeviceConnectionLost += ProximityMonitorManager_DeviceConnectionLost;

            _deviceReconnectManager.DeviceDisconnected += DeviceReconnectManager_DeviceDisconnected;
        }

        void FlowProcessor_DeviceFinalizingMainFlow(object sender, IDevice device)
        {
            if (device == null)
                return;

            lock (_deviceListsLock)
            {
                if (!(device is IRemoteDeviceProxy) && !device.IsBoot)
                {
                    // Limit of one device that may be authorized for workstation lock
                    if (_deviceManager.Devices.Any(d => d.IsConnected && d.Id != device.Id && d.GetUserProperty<bool>(PROX_LOCK_ENABLED_PROP)))
                    {
                        device.SetUserProperty(PROX_LOCK_ENABLED_PROP, false);
                    }
                    else
                    {
                        WriteLine($"Vault ({device.Id}) added as valid to trigger workstation lock");
                        device.SetUserProperty(PROX_LOCK_ENABLED_PROP, true);
                        SafeInvoke(DeviceProxLockEnabled, device);
                    }
                }
            }
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
                _flowProcessor.DeviceFinishedMainFlow -= FlowProcessor_DeviceFinalizingMainFlow;

                _proximityMonitorManager.DeviceConnectionLost -= ProximityMonitorManager_DeviceConnectionLost;
                _proximityMonitorManager.DeviceBelowLockForToLong -= ProximityMonitorManager_DeviceBelowLockForToLong;
                _proximityMonitorManager.DeviceProximityTimeout -= ProximityMonitorManager_DeviceProximityTimeout;
            }

            disposed = true;
        }

        ~WorkstationLockProcessor()
        {
            Dispose(false);
        }
        #endregion

        public bool IsEnabled { get; private set; }

        public void Start()
        {
            IsEnabled = true;
        }

        public void Stop()
        {
            IsEnabled = false;
        }

        void ProximityMonitorManager_DeviceBelowLockForToLong(object sender, IDevice device)
        {
            lock (_deviceListsLock)
            {
                if (CanLock(device))
                {
                    WriteLine($"Going to lock the workstation by 'DeviceBelowLockForToLong' reason. Vault ID: {device.Id}");
                    WorkstationLocking?.Invoke(this, new WorkstationLockingEventArgs(device, WorkstationLockingReason.DeviceBelowThreshold));
                    _workstationLocker.LockWorkstation();
                    device.SetUserProperty(PROX_LOCK_ENABLED_PROP, false);
                }
            }
        }

        void ProximityMonitorManager_DeviceProximityTimeout(object sender, IDevice device)
        {
            lock (_deviceListsLock)
            {
                if (CanLock(device))
                {
                    WriteLine($"Going to lock the workstation by 'DeviceProximityTimeout' reason. Vault ID: {device.Id}");
                    WorkstationLocking?.Invoke(this, new WorkstationLockingEventArgs(device, WorkstationLockingReason.ProximityTimeout));
                    _workstationLocker.LockWorkstation();
                    device.SetUserProperty(PROX_LOCK_ENABLED_PROP, false);
                }
            }
        }

        void ProximityMonitorManager_DeviceConnectionLost(object sender, IDevice device)
        {
            lock (_deviceListsLock)
            {
                if (!_deviceReconnectManager.IsEnabled && CanLock(device))
                {
                    WriteLine($"Going to lock the workstation by 'DeviceConnectionLost' reason. Vault ID: {device.Id}");
                    WorkstationLocking?.Invoke(this, new WorkstationLockingEventArgs(device, WorkstationLockingReason.DeviceConnectionLost));
                    _workstationLocker.LockWorkstation();
                    device.SetUserProperty(PROX_LOCK_ENABLED_PROP, false);
                }
            }
        }

        void DeviceReconnectManager_DeviceDisconnected(object sender, IDevice device)
        {
            lock (_deviceListsLock)
            {
                if (_deviceReconnectManager.IsEnabled && CanLock(device))
                {
                    WriteLine($"Going to lock the workstation by 'DeviceConnectionLost' reason. Vault ID: {device.Id}");
                    WorkstationLocking?.Invoke(this, new WorkstationLockingEventArgs(device, WorkstationLockingReason.DeviceConnectionLost));
                    _workstationLocker.LockWorkstation();
                    device.SetUserProperty(PROX_LOCK_ENABLED_PROP, false);
                }
            }
        }

        bool CanLock(IDevice device)
        {
            return IsEnabled && device.GetUserProperty<bool>(PROX_LOCK_ENABLED_PROP) && _proximitySettingsProvider.IsEnabledLockByProximity(device.DeviceConnection.Connection.ConnectionId);
        }
    }
}
