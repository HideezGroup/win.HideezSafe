using System;
using System.Threading.Tasks;
using Hideez.SDK.Communication.Log;
using Meta.Lib.Modules.PubSub;
using HideezMiddleware.Modules.Rfid.Messages;
using HideezMiddleware.Modules.RemoteUnlock.Messages;
using HideezMiddleware.Modules.Hes.Messages;
using HideezMiddleware.Modules.WinBle.Messages;
using HideezMiddleware.Modules.Csr.Messages;
using Hideez.SDK.Communication.HES.Client;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.Modules.CredentialProvider.Messages;

namespace HideezMiddleware
{
    public class StatusManager : Logger
    {
        readonly IClientUiManager _uiClientManager;
        readonly IMetaPubSub _messenger;

        RfidStatus rfidStatus = RfidStatus.Disabled;
        BluetoothStatus csrStatus = BluetoothStatus.Disabled;
        BluetoothStatus winBleStatus = BluetoothStatus.Disabled;
        HesStatus hesStatus = HesStatus.Disabled;
        HesStatus tbStatus = HesStatus.Disabled;

        public StatusManager(IClientUiManager clientUiManager,
            IMetaPubSub messenger,
            ILog log)
            : base(nameof(StatusManager), log)
        {
            _uiClientManager = clientUiManager;
            _messenger = messenger;

            _messenger.Subscribe<RfidStatusChangedMessage>(ServiceStatusChanged);
            _messenger.Subscribe<CsrStatusChangedMessage>(BleAdapterStatusChanged);
            _messenger.Subscribe<WinBleStatusChangedMessage>(WinBleAdapterStatusChanged);
            _messenger.Subscribe<HesAppConnection_HubConnectionStateChangedMessage>(HesConnectionStateChanged);
            _messenger.Subscribe<TBConnection_StateChangedMessage>(TBConnectionStateChanged);
            _messenger.Subscribe<CredentialProvider_ConnectedMessage>(WorkstationUnlockerConnected);
            _messenger.Subscribe<RefreshStatusMessage>(RefreshStatus);
        }

        private async Task ServiceStatusChanged(RfidStatusChangedMessage msg)
        {
            rfidStatus = msg.Status;
            await SendStatusToUI();
        }

        private async Task WinBleAdapterStatusChanged(WinBleStatusChangedMessage msg)
        {
            winBleStatus = msg.Status;
            await SendStatusToUI();
        }

        private async Task BleAdapterStatusChanged(CsrStatusChangedMessage msg)
        {
            csrStatus = msg.Status;
            await SendStatusToUI();
        }

        private async Task HesConnectionStateChanged(HesAppConnection_HubConnectionStateChangedMessage msg)
        {
            var hesConnection = (IHesAppConnection)msg.Sender;

            if (hesConnection.State == HesConnectionState.Connected)
                hesStatus = HesStatus.Ok;
            else if (hesConnection.State == HesConnectionState.NotApproved)
                hesStatus = HesStatus.NotApproved;
            else
                hesStatus = HesStatus.HesNotConnected;

            await SendStatusToUI();
        }

        private async Task TBConnectionStateChanged(TBConnection_StateChangedMessage msg)
        {
            var tbConnection = (IHesAppConnection)msg.Sender;

            if (tbConnection.State == HesConnectionState.Connected)
                tbStatus = HesStatus.Ok;
            else if (tbConnection.State == HesConnectionState.NotApproved)
                tbStatus = HesStatus.NotApproved;
            else
                tbStatus = HesStatus.HesNotConnected;

            await SendStatusToUI();
        }

        private async Task WorkstationUnlockerConnected(CredentialProvider_ConnectedMessage arg)
        {
            await Task.Delay(200);
            await SendStatusToUI();
        }

        public async Task SendStatusToUI()
        {
            try
            {
                await _uiClientManager.SendStatus(hesStatus, rfidStatus, csrStatus, winBleStatus, tbStatus);
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        private async Task RefreshStatus(RefreshStatusMessage arg)
        {
            await SendStatusToUI();
        }
    }
}
