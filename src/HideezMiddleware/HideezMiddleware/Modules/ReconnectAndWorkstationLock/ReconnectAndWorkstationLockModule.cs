using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.IPC.DTO;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.Modules.DeviceManagement.Messages;
using HideezMiddleware.Modules.Hes.Messages;
using HideezMiddleware.Modules.ServiceEvents.Messages;
using HideezMiddleware.ReconnectManager;
using HideezMiddleware.Utils.WorkstationHelper;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.ReconnectAndWorkstationLock
{
    public sealed class WorkstationLockModule : ModuleBase
    {
        readonly DeviceReconnectManager _deviceReconnectManager;
        readonly WorkstationLockProcessor _workstationLockProcessor;
        readonly UniversalWorkstationLocker _universalWorkstationLocker;

        public WorkstationLockModule(DeviceReconnectManager deviceReconnectManager,
            IWorkstationHelper workstationHelper,
            ConnectionFlowProcessorBase connectionFlowProcessor,
            ProximityMonitorManager proximityMonitorManager,
            DeviceManager deviceManager,
            IDeviceProximitySettingsProvider proximitySettingsProvider,
            IMetaPubSub messenger,
            ILog log)
            : base(messenger, nameof(WorkstationLockModule), log)
        {
            _deviceReconnectManager = deviceReconnectManager;
            _universalWorkstationLocker = new UniversalWorkstationLocker(SdkConfig.DefaultLockTimeout * 1000, messenger, workstationHelper, log);

            _workstationLockProcessor = new WorkstationLockProcessor(connectionFlowProcessor,
                proximityMonitorManager,
                deviceManager,
                _universalWorkstationLocker,
                proximitySettingsProvider,
                _deviceReconnectManager,
                log);

            _workstationLockProcessor.DeviceProxLockEnabled += WorkstationLockProcessor_DeviceProxLockEnabled;

            SessionSwitchMonitor.SessionSwitch += SessionSwitchMonitor_SessionSwitch;

            _messenger.Subscribe(GetSafeHandler<HesAccessManager_AccessRetractedMessage>(HesAccessManager_AccessRetracted));
            _messenger.Subscribe(GetSafeHandler<DeviceManager_ExpectedDeviceRemovalMessage>(DeviceManager_UserRemovingDevice));
            _messenger.Subscribe(GetSafeHandler<HesAppConnection_AlarmMessage>(HesAppConnection_Alarm));

            proximityMonitorManager.Start();
            //_deviceReconnectManager.Start(); // Reconnect feature is disabled until further notice
            _workstationLockProcessor.Start();
        }

        private async void WorkstationLockProcessor_DeviceProxLockEnabled(object sender, IDevice device)
        {
            await SafePublish(new DeviceProximityLockEnabledMessage(new DeviceDTO(device)));
        }

        // Disable automatic reconnect when user is logged out or session is locked
        private void SessionSwitchMonitor_SessionSwitch(int sessionId, SessionSwitchReason reason)
        {
            if (reason == SessionSwitchReason.SessionLogoff || reason == SessionSwitchReason.SessionLock)
            {
                _deviceReconnectManager.DisableAllDevicesReconnect();
            }
        }

        // Disable automatic reconnect when access is retracted
        private Task HesAccessManager_AccessRetracted(HesAccessManager_AccessRetractedMessage msg)
        {
            _deviceReconnectManager.DisableAllDevicesReconnect();

            return Task.CompletedTask;
        }

        // Disable automatic reconnect when device removal is expected, due to user actions or some events
        private Task DeviceManager_UserRemovingDevice(DeviceManager_ExpectedDeviceRemovalMessage msg)
        {
            _deviceReconnectManager.DisableDeviceReconnect(msg.Device);

            return Task.CompletedTask;
        }

        private Task HesAppConnection_Alarm(HesAppConnection_AlarmMessage arg)
        {
            _universalWorkstationLocker.LockWorkstation();
            return Task.CompletedTask;
        }
    }
}
