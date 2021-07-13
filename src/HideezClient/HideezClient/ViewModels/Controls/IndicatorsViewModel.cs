using GalaSoft.MvvmLight.Messaging;
using Hideez.SDK.Communication.Log;
using HideezClient.Controls;
using HideezClient.Messages;
using HideezClient.Models.Settings;
using HideezClient.Modules.Log;
using HideezClient.Modules.ServiceProxy;
using HideezClient.Mvvm;
using HideezMiddleware;
using HideezMiddleware.ConnectionModeProvider;
using HideezMiddleware.Settings;
using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.ObjectModel;
using System.ServiceModel;
using System.Threading.Tasks;

namespace HideezClient.ViewModels.Controls
{
    class IndicatorsViewModel : ObservableObject
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger(nameof(IndicatorsViewModel));
        private readonly IServiceProxy _serviceProxy;
        private readonly IConnectionModeProvider _connectionModeProvider;
        private StateControlViewModel service;
        private StateControlViewModel server;
        private StateControlViewModel rfid;
        private StateControlViewModel dongle;
        private StateControlViewModel tbServer;
        private StateControlViewModel bluetooth;

        public IndicatorsViewModel(IServiceProxy serviceProxy, 
            ISettingsManager<ApplicationSettings> appSettingsManager, 
            IConnectionModeProvider connectionModeProvider, 
            IMetaPubSub metaMessenger)
        {
            _serviceProxy = serviceProxy;
            _connectionModeProvider = connectionModeProvider;
            InitIndicators();

            metaMessenger.Subscribe<ConnectionServiceChangedMessage>(c => ResetIndicators(c.IsConnected));
            metaMessenger.TrySubscribeOnServer<HideezMiddleware.IPC.Messages.ServiceComponentsStateChangedMessage>(OnComponentsStateChangedMessage);
        }

        #region Properties

        public ObservableCollection<StateControlViewModel> Indicators { get; } = new ObservableCollection<StateControlViewModel>();

        public StateControlViewModel Service
        {
            get { return service; }
            set { Set(ref service, value); }
        }

        public StateControlViewModel Server
        {
            get { return server; }
            set { Set(ref server, value); }
        }

        public StateControlViewModel RFID
        {
            get { return rfid; }
            set { Set(ref rfid, value); }
        }

        public StateControlViewModel Dongle
        {
            get { return dongle; }
            set { Set(ref dongle, value); }
        }

        public StateControlViewModel TBServer
        {
            get { return tbServer; }
            set { Set(ref tbServer, value); }
        }

        public StateControlViewModel Bluetooth
        {
            get { return bluetooth; }
            set { Set(ref bluetooth, value); }
        }

        #endregion
        
        Task OnComponentsStateChangedMessage(HideezMiddleware.IPC.Messages.ServiceComponentsStateChangedMessage msg)
        {
            _log.WriteLine("Updating components state indicators");
            // Service
            Service.Visible = !_serviceProxy.IsConnected;

            // HES
            switch (msg.HesStatus)
            {
                case HesStatus.Ok:
                    Server.State = StateControlState.Green;
                    break;
                case HesStatus.NotApproved:
                    Server.State = StateControlState.Orange;
                    break;
                default:
                    Server.State = StateControlState.Red;
                    break;
            }
            Server.Visible = msg.HesStatus != HesStatus.Disabled && _serviceProxy.IsConnected;

            // RFID
            RFID.State = StateControlViewModel.BoolToState(msg.RfidStatus == RfidStatus.Ok);
            RFID.Visible = msg.RfidStatus != RfidStatus.Disabled && _serviceProxy.IsConnected && msg.RfidStatus == RfidStatus.Ok;

            // Hideez Dongle
            Dongle.State = StateControlViewModel.BoolToState(msg.DongleStatus == BluetoothStatus.Ok);
            Dongle.Visible = _serviceProxy.IsConnected && _connectionModeProvider.IsCsrMode;

            // Built-in Bluetooth
            Bluetooth.State = StateControlViewModel.BoolToState(msg.BluetoothStatus == BluetoothStatus.Ok);
            Bluetooth.Visible = _serviceProxy.IsConnected && _connectionModeProvider.IsWinBleMode;

            // Try&Buy Server
            TBServer.State = StateControlViewModel.BoolToState(msg.TbHesStatus == HesStatus.Ok);
            TBServer.Visible = _serviceProxy.IsConnected;

            return Task.CompletedTask;
        }

        private void InitIndicators()
        {
            Service = new StateControlViewModel
            {
                Name = "Status.Service",
                GreenTooltip = "Status.Tooltip.ConectedService",
                RedTooltip = "Status.Tooltip.DisconectedService",
                Visible = true,
            };

            Server = new StateControlViewModel
            {
                Name = "Status.Server",
                GreenTooltip = "Status.Tooltip.ConectedServer",
                OrangeTooltip = "Status.Tooltip.NotApprovedServer",
                RedTooltip = "Status.Tooltip.DisconectedServer",
            };

            RFID = new StateControlViewModel
            {
                Name = "Status.RFID",
                GreenTooltip = "Status.Tooltip.ConectedRFID",
                RedTooltip = "Status.Tooltip.DisconectedRFID",
            };

            Dongle = new StateControlViewModel
            {
                Name = "Status.Dongle",
                GreenTooltip = "Status.Tooltip.ConectedDongle",
                RedTooltip = "Status.Tooltip.DisconectedDongle",
            };

            TBServer = new StateControlViewModel
            {
                Name = "Status.Network",
                GreenTooltip = "Status.Tooltip.NetworkAvailable",
                RedTooltip = "Status.Tooltip.NetworkUnavailable",
            };

            Bluetooth = new StateControlViewModel
            {
                Name = "Status.Bluetooth",
                GreenTooltip = "Status.Tooltip.BluetoothAvailable",
                RedTooltip = "Status.Tooltip.BluetoothUnavailable",
            };

            Indicators.Add(Server);
            Indicators.Add(RFID);
            Indicators.Add(Dongle);
            Indicators.Add(Bluetooth);
        }

        private Task ResetIndicators(bool isServiceConnected)
        {
            if (isServiceConnected)
            {
                Service.State = StateControlState.Green;
                Service.Visible = false;
            }   
            else
            {
                Service.State = StateControlState.Red;
                Service.Visible = true;
            }

            Server.State = StateControlState.Red;
            Server.Visible = false;

            Dongle.State = StateControlState.Red;
            Dongle.Visible = false;

            RFID.State = StateControlState.Red;
            RFID.Visible = false;

            TBServer.State = StateControlState.Red;
            TBServer.Visible = false;

            Bluetooth.State = StateControlState.Red;
            Bluetooth.Visible = false;

            return Task.CompletedTask;
        }
    }
}
