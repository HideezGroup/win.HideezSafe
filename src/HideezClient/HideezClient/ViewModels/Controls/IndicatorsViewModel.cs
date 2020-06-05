﻿using GalaSoft.MvvmLight.Messaging;
using Hideez.SDK.Communication.Log;
using HideezClient.Controls;
using HideezClient.Messages;
using HideezClient.Modules.Log;
using HideezClient.Modules.ServiceProxy;
using HideezClient.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ServiceModel;

namespace HideezClient.ViewModels
{
    class IndicatorsViewModel : ObservableObject
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger(nameof(IndicatorsViewModel));
        private readonly IMessenger _messenger;
        private readonly IServiceProxy _serviceProxy;

        private StateControlViewModel service;
        private StateControlViewModel server;
        private StateControlViewModel rfid;
        private StateControlViewModel dongle;
        private StateControlViewModel tbServer;

        public IndicatorsViewModel(IMessenger messenger, IServiceProxy serviceProxy)
        {
            _messenger = messenger;
            _serviceProxy = serviceProxy;

            InitIndicators();

            messenger.Register<ConnectionServiceChangedMessage>(this, c => ResetIndicators(c.IsConnected));
            messenger.Register<ServiceComponentsStateChangedMessage>(this, OnComponentsStateChangedMessage);
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

        #endregion
        
        void OnComponentsStateChangedMessage(ServiceComponentsStateChangedMessage msg)
        {
            _log.WriteLine("Updating components state indicators");
            // Service
            Service.Visible = !_serviceProxy.IsConnected;

            // HES
            switch (msg.HesStatus)
            {
                case HideezServiceReference.HesStatus.Ok:
                    Server.State = StateControlState.Green;
                    break;
                case HideezServiceReference.HesStatus.NotApproved:
                    Server.State = StateControlState.Orange;
                    break;
                default:
                    Server.State = StateControlState.Red;
                    break;
            }
            Server.Visible = _serviceProxy.IsConnected;

            // RFID
            RFID.State = StateControlViewModel.BoolToState(msg.RfidStatus == HideezServiceReference.RfidStatus.Ok);
            RFID.Visible = msg.RfidStatus != HideezServiceReference.RfidStatus.Disabled && _serviceProxy.IsConnected;

            // Bluetooth
            Dongle.State = StateControlViewModel.BoolToState(msg.BluetoothStatus == HideezServiceReference.BluetoothStatus.Ok);
            Dongle.Visible = _serviceProxy.IsConnected;

            // Try&Buy Server
            TBServer.State = StateControlViewModel.BoolToState(msg.TbHesStatus == HideezServiceReference.HesStatus.Ok);
            TBServer.Visible = _serviceProxy.IsConnected;
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

            Indicators.Add(Server);
            Indicators.Add(RFID);
            Indicators.Add(Dongle);
        }

        private void ResetIndicators(bool isServiceConnected)
        {
            if (isServiceConnected)
            {
                Service.State = StateControlState.Green;
                Service.Visible = false;

                Server.Visible = true;
                Dongle.Visible = true;

                TBServer.Visible = true;
            }   
            else
            {
                Service.State = StateControlState.Red;
                Service.Visible = true;

                Server.State = StateControlState.Red;
                Server.Visible = false;

                Dongle.State = StateControlState.Red;
                Dongle.Visible = false;

                RFID.State = StateControlState.Red;
                RFID.Visible = false;

                TBServer.State = StateControlState.Red;
                TBServer.Visible = false;
            }
        }
    }
}
