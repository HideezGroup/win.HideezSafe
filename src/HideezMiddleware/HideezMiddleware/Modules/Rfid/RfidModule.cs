using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.Other;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.Modules.Rfid.Messages;
using HideezMiddleware.Modules.ServiceEvents.Messages;
using Meta.Lib.Modules.PubSub;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.Rfid
{
    public sealed class RfidModule : ModuleBase
    {
        readonly RfidConnectionProcessor _rfidConnectionProcessor;
        readonly RfidServiceConnection _rfidServiceConnection;

        public RfidModule(RfidConnectionProcessor rfidConnectionProcessor, 
            RfidServiceConnection rfidServiceConnection, 
            IMetaPubSub messenger, 
            ILog log)
            : base(messenger, nameof(RfidModule), log)
        {

            _rfidConnectionProcessor = rfidConnectionProcessor;
            _rfidServiceConnection = rfidServiceConnection;

            _rfidServiceConnection.RfidReaderStateChanged += RfidServiceConnection_RfidReaderStateChanged;

            _messenger.Subscribe(GetSafeHandler<PowerEventMonitor_SystemSuspendingMessage>(OnSysteSuspending));
            _messenger.Subscribe(GetSafeHandler<PowerEventMonitor_SystemLeftSuspendedModeMessage>(OnSystemLeftSuspendedMode));

            _rfidServiceConnection.Start();
            _rfidConnectionProcessor.Start();
        }

        private async void RfidServiceConnection_RfidReaderStateChanged(object sender, EventArgs e)
        {
            RfidStatus status;
            if (!_rfidServiceConnection.ServiceConnected)
                status = RfidStatus.RfidServiceNotConnected;
            else if (!_rfidServiceConnection.ReaderConnected)
                status = RfidStatus.RfidReaderNotConnected;
            else
                status = RfidStatus.Ok;

            await SafePublish(new RfidStatusChangedMessage(sender, status));
        }

        private Task OnSysteSuspending(PowerEventMonitor_SystemSuspendingMessage arg)
        {
            _rfidConnectionProcessor.Stop();
            return Task.CompletedTask;
        }

        private Task OnSystemLeftSuspendedMode(PowerEventMonitor_SystemLeftSuspendedModeMessage msg)
        {
            WriteLine("Starting restore from suspended mode");

            _rfidConnectionProcessor.Stop();
            _rfidConnectionProcessor.Start();
            return Task.CompletedTask;
        }
    }
}
