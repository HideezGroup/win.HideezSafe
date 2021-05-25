using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.CredentialProvider;
using HideezMiddleware.DeviceConnection;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.Other;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.WinBle;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.Modules.ServiceEvents.Messages;
using HideezMiddleware.Modules.WinBle.Messages;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using WinBle;

namespace HideezMiddleware.Modules.WinBle
{
    public sealed class WinBleModule : ModuleBase
    {
        readonly AdvertisementIgnoreList _advertisementIgnoreList;
        readonly WinBleConnectionManagerWrapper _winBleConnectionManagerWrapper;
        readonly ActivityConnectionProcessor _activityConnectionProcessor;
        readonly AutomaticConnectionProcessor _winBleAutomaticConnectionProcessor;
        readonly CommandLinkVisibilityController _commandLinkVisibilityController;
        readonly ConnectionManagerRestarter _connectionManagerRestarter;

        public WinBleModule(ConnectionManagersCoordinator connectionManagersCoordinator,
            ConnectionManagerRestarter connectionManagerRestarter,
            AdvertisementIgnoreList advertisementIgnoreList,
            WinBleConnectionManagerWrapper winBleConnectionManagerWrapper,
            ActivityConnectionProcessor activityConnectionProcessor,
            AutomaticConnectionProcessor winBleAutomaticConnectionProcessor,
            CommandLinkVisibilityController commandLinkVisibilityController,
            IMetaPubSub messenger,
            ILog log)
            : base(messenger, nameof(WinBleModule), log)
        {
            _advertisementIgnoreList = advertisementIgnoreList;
            _winBleConnectionManagerWrapper = winBleConnectionManagerWrapper;
            _activityConnectionProcessor = activityConnectionProcessor;
            _winBleAutomaticConnectionProcessor = winBleAutomaticConnectionProcessor;
            _commandLinkVisibilityController = commandLinkVisibilityController;
            _connectionManagerRestarter = connectionManagerRestarter;

            SessionSwitchMonitor.SessionSwitch += SessionSwitchMonitor_SessionSwitch;
            _winBleConnectionManagerWrapper.AdapterStateChanged += WinBleConnectionManager_AdapterStateChanged;
            _winBleConnectionManagerWrapper.ControllerRemoved += BleConnectionManager_ControllerRemoved;

            _connectionManagerRestarter.AddManager(_winBleConnectionManagerWrapper);
            connectionManagersCoordinator.AddConnectionManager(_winBleConnectionManagerWrapper);

            _messenger.Subscribe(GetSafeHandler<PowerEventMonitor_SystemSuspendingMessage>(OnSystemSuspending));
            _messenger.Subscribe(GetSafeHandler<PowerEventMonitor_SystemLeftSuspendedModeMessage>(OnSystemLeftSuspendedMode));

            _activityConnectionProcessor.Start();
            _winBleAutomaticConnectionProcessor.Start();
        }

        private void SessionSwitchMonitor_SessionSwitch(int sessionId, SessionSwitchReason reason)
        {
            switch (reason)
            {
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.SessionUnlock:
                    _advertisementIgnoreList.Clear();
                    break;
                default:
                    break;
            }
        }

        private async void WinBleConnectionManager_AdapterStateChanged(object sender, EventArgs e)
        {
            BluetoothStatus status;
            switch (_winBleConnectionManagerWrapper.State)
            {
                case BluetoothAdapterState.PoweredOn:
                case BluetoothAdapterState.LoadingKnownDevices:
                    status = BluetoothStatus.Ok;
                    break;
                case BluetoothAdapterState.Unknown:
                    status = BluetoothStatus.Unknown;
                    break;
                case BluetoothAdapterState.Resetting:
                    status = BluetoothStatus.Resetting;
                    break;
                case BluetoothAdapterState.Unsupported:
                    status = BluetoothStatus.Unsupported;
                    break;
                case BluetoothAdapterState.Unauthorized:
                    status = BluetoothStatus.Unauthorized;
                    break;
                case BluetoothAdapterState.PoweredOff:
                    status = BluetoothStatus.PoweredOff;
                    break;
                default:
                    status = BluetoothStatus.Unknown;
                    break;
            }

            await SafePublish(new WinBleStatusChangedMessage(sender, status));
        }

        private void BleConnectionManager_ControllerRemoved(object sender, ControllerRemovedEventArgs e)
        {
            _advertisementIgnoreList.Remove(e.Controller.Id);
        }

        private Task OnSystemSuspending(PowerEventMonitor_SystemSuspendingMessage arg)
        {
            _activityConnectionProcessor.Stop();
            _winBleAutomaticConnectionProcessor.Stop();
            return Task.CompletedTask;
        }

        private async Task OnSystemLeftSuspendedMode(PowerEventMonitor_SystemLeftSuspendedModeMessage msg)
        {
            WriteLine("Starting restore from suspended mode");

            await _winBleConnectionManagerWrapper.Stop();
            _activityConnectionProcessor.Stop();
            _winBleAutomaticConnectionProcessor.Stop();

            _activityConnectionProcessor.Start();
            _winBleAutomaticConnectionProcessor.Start();
            await _winBleConnectionManagerWrapper.Start();

            _advertisementIgnoreList.Clear();
        }
    

    }
}
