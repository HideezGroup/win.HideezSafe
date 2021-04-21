﻿using Hideez.CsrBLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.Modules.Csr.Messages;
using HideezMiddleware.Modules.ServiceEvents.Messages;
using Meta.Lib.Modules.PubSub;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.Csr
{
    public sealed class CsrModule : ModuleBase
    {
        private readonly BleConnectionManager _csrBleConnectionManager;
        private readonly TapConnectionProcessor _tapConnectionProcessor;
        private readonly ProximityConnectionProcessor _proximityConnectionProcessor;
        private readonly ConnectionManagerRestarter _connectionManagerRestarter;

        public CsrModule(ConnectionManagersCoordinator connectionManagersCoordinator,
            ConnectionManagerRestarter connectionManagerRestarter,
            BleConnectionManager csrBleConnectionManager,
            TapConnectionProcessor tapConnectionProcessor,
            ProximityConnectionProcessor proximityConnectionProcessor,
            IMetaPubSub messenger,
            ILog log)
            : base(messenger, nameof(CsrModule), log)
        {
            _csrBleConnectionManager = csrBleConnectionManager;
            _tapConnectionProcessor = tapConnectionProcessor;
            _proximityConnectionProcessor = proximityConnectionProcessor;
            _connectionManagerRestarter = connectionManagerRestarter;

            _csrBleConnectionManager.AdapterStateChanged += CsrBleConnectionManager_AdapterStateChanged;
            _csrBleConnectionManager.DiscoveryStopped += (s, e) => { }; // Event requires to have at least one handler
            _csrBleConnectionManager.DiscoveredDeviceAdded += (s, e) => { }; // Event requires intended to have at least one handler
            _csrBleConnectionManager.DiscoveredDeviceRemoved += (s, e) => { }; // Event requires intended to have at least one handler

            _messenger.Subscribe(GetSafeHandler<PowerEventMonitor_SystemSuspendingMessage>(OnSysteSuspending));
            _messenger.Subscribe(GetSafeHandler<PowerEventMonitor_SystemLeftSuspendedModeMessage>(OnSystemLeftSuspendedMode));

            _connectionManagerRestarter.AddManager(_csrBleConnectionManager);
            connectionManagersCoordinator.AddConnectionManager(_csrBleConnectionManager);

            _tapConnectionProcessor.Start();
            _proximityConnectionProcessor.Start();
        }

        private async void CsrBleConnectionManager_AdapterStateChanged(object sender, EventArgs e)
        {
            BluetoothStatus status;
            switch (_csrBleConnectionManager.State)
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

            await SafePublish(new CsrStatusChangedMessage(sender, status));
        }

        private Task OnSysteSuspending(PowerEventMonitor_SystemSuspendingMessage arg)
        {
            _proximityConnectionProcessor.Stop();
            _tapConnectionProcessor.Stop();
            return Task.CompletedTask;
        }

        private async Task OnSystemLeftSuspendedMode(PowerEventMonitor_SystemLeftSuspendedModeMessage msg)
        {
            WriteLine("Starting restore from suspended mode"); 
            
            _proximityConnectionProcessor.Stop();
            _tapConnectionProcessor.Stop();

            _connectionManagerRestarter.Stop();
            await Task.Run(async() =>
            {
                await Task.Delay(1000);
                WriteLine("Restarting connection manager");
                await _csrBleConnectionManager.Restart();
                _connectionManagerRestarter.Start();

                WriteLine("Starting connection processors");
                _proximityConnectionProcessor.Start();
                _tapConnectionProcessor.Start();
            });
        }
    }
}
