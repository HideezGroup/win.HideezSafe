﻿using Hideez.CsrBLE;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Workstation;
using Hideez.SDK.Communication.WorkstationEvents;
using HideezMiddleware.Audit;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.Modules.Csr.Messages;
using HideezMiddleware.Modules.DeviceManagement.Messages;
using HideezMiddleware.Modules.Hes.Messages;
using HideezMiddleware.Modules.Rfid.Messages;
using HideezMiddleware.Modules.ServiceEvents.Messages;
using HideezMiddleware.Workstation;
using Meta.Lib.Modules.PubSub;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.Audit
{
    public sealed class AuditModule : ModuleBase
    {
        readonly IWorkstationIdProvider _workstationIdProvider;
        readonly ISessionInfoProvider _sessionInfoProvider;
        readonly EventSaver _eventSaver;
        readonly EventSender _eventSender;

        public AuditModule(IWorkstationIdProvider workstationIdProvider,
            ISessionInfoProvider sessionInfoProvider,
            EventSaver eventSaver,
            EventSender eventSender,
            IMetaPubSub messenger, 
            ILog log)
            : base(messenger, nameof(AuditModule), log)
        {
            _workstationIdProvider = workstationIdProvider;
            _sessionInfoProvider = sessionInfoProvider;
            _eventSaver = eventSaver;
            _eventSender = eventSender;

            // TODO: Remove SaveWorkstationId;  instead WorkstationId should be saved once on application setup
            if (string.IsNullOrWhiteSpace(workstationIdProvider.GetWorkstationId()))
                workstationIdProvider.SaveWorkstationId(Guid.NewGuid().ToString());

            _messenger.Subscribe(GetSafeHandler<CsrStatusChangedMessage>(HandleCsrStatusChanged));
            _messenger.Subscribe(GetSafeHandler<RfidStatusChangedMessage>(HandleRfidStatusChanged));
            _messenger.Subscribe(GetSafeHandler<HesAppConnection_HubConnectionStateChangedMessage>(HandleHubConnectionStateChanged));
            _messenger.Subscribe(GetSafeHandler<DeviceInitializedMessage>(DeviceInitialized));
            _messenger.Subscribe(GetSafeHandler<DeviceDisconnectedMessage>(DeviceDisconnected));
            _messenger.Subscribe(GetSafeHandler<DeviceManager_DeviceRemovedMessage>(DeviceRemoved));
            _messenger.Subscribe(GetSafeHandler<PowerEventMonitor_SystemSuspendingMessage>(OnSystemSuspending));

            _messenger.Subscribe(GetSafeHandler<PublishEventMessage>(PublishEvent));
        }

        private async Task HandleCsrStatusChanged(CsrStatusChangedMessage msg)
        {
            var csrBleConnectionManager = (BleConnectionManager)msg.Sender;
            if (csrBleConnectionManager.State == BluetoothAdapterState.Unknown || csrBleConnectionManager.State == BluetoothAdapterState.PoweredOn)
            {
                var we = _eventSaver.GetWorkstationEvent();
                if (csrBleConnectionManager.State == BluetoothAdapterState.PoweredOn)
                {
                    we.EventId = WorkstationEventType.DonglePlugged;
                    we.Severity = WorkstationEventSeverity.Info;
                }
                else
                {
                    we.EventId = WorkstationEventType.DongleUnplugged;
                    we.Severity = WorkstationEventSeverity.Warning;
                }

                await _eventSaver.AddNewAsync(we);
            }
        }

        private bool prevRfidIsConnectedState = false;
        private async Task HandleRfidStatusChanged(RfidStatusChangedMessage msg)
        {
            var rfidServiceConnection = (RfidServiceConnection)msg.Sender;
            var isConnected = rfidServiceConnection.ServiceConnected && rfidServiceConnection.ReaderConnected;
            if (prevRfidIsConnectedState != isConnected)
            {
                prevRfidIsConnectedState = isConnected;

                var we = _eventSaver.GetWorkstationEvent();
                we.EventId = isConnected ? WorkstationEventType.RFIDAdapterPlugged : WorkstationEventType.RFIDAdapterUnplugged;
                we.Severity = isConnected ? WorkstationEventSeverity.Info : WorkstationEventSeverity.Warning;

                await _eventSaver.AddNewAsync(we);
            }
        }

        private bool prevHesIsConnectedState = false;
        private async Task HandleHubConnectionStateChanged(HesAppConnection_HubConnectionStateChangedMessage msg)
        {
            var hesConnection = msg.Sender as HesAppConnection;
            var isConnected = hesConnection.State == HesConnectionState.Connected;
            if (prevHesIsConnectedState != isConnected)
            {
                prevHesIsConnectedState = isConnected;
                bool sendImmediately = false;
                var we = _eventSaver.GetWorkstationEvent();
                if (hesConnection.State == HesConnectionState.Connected)
                {
                    we.EventId = WorkstationEventType.HESConnected;
                    we.Severity = WorkstationEventSeverity.Info;
                    sendImmediately = true;
                }
                else
                {
                    we.EventId = WorkstationEventType.HESDisconnected;
                    we.Severity = WorkstationEventSeverity.Warning;
                }

                await _eventSaver.AddNewAsync(we, sendImmediately);
            }
        }

        private async Task DeviceInitialized(DeviceInitializedMessage msg)
        {
            var workstationEvent = _eventSaver.GetWorkstationEvent();
            workstationEvent.Severity = WorkstationEventSeverity.Info;
            workstationEvent.DeviceId = msg.Device.SerialNo;
            if (msg.Device.IsRemote)
            {
                workstationEvent.EventId = WorkstationEventType.RemoteConnect;
            }
            else
            {
                workstationEvent.EventId = WorkstationEventType.DeviceConnect;
            }
            await _eventSaver.AddNewAsync(workstationEvent);
        }

        private async Task DeviceDisconnected(DeviceDisconnectedMessage msg)
        {
            if (msg.Device.IsInitialized)
            {
                var workstationEvent = _eventSaver.GetWorkstationEvent();
                workstationEvent.Severity = WorkstationEventSeverity.Info;
                workstationEvent.DeviceId = msg.Device.SerialNo;
                if (msg.Device.IsRemote)
                {
                    workstationEvent.EventId = WorkstationEventType.RemoteDisconnect;
                }
                else
                {
                    workstationEvent.EventId = WorkstationEventType.DeviceDisconnect;
                }
                await _eventSaver.AddNewAsync(workstationEvent);
            }
        }

        private async Task DeviceRemoved(DeviceManager_DeviceRemovedMessage msg)
        {
            if (!(msg.Device is IRemoteDeviceProxy) && msg.Device.IsInitialized)
            {
                var workstationEvent = _eventSaver.GetWorkstationEvent();
                workstationEvent.EventId = WorkstationEventType.DeviceDeleted;
                workstationEvent.Severity = WorkstationEventSeverity.Warning;
                workstationEvent.DeviceId = msg.Device.SerialNo;
                await _eventSaver.AddNewAsync(workstationEvent);
            }
        }

        private async Task OnSystemSuspending(PowerEventMonitor_SystemSuspendingMessage arg)
        {
            WriteLine("Sending all events");
            await _eventSender.SendEventsAsync(true);
        }

        private async Task PublishEvent(PublishEventMessage msg)
        {
            var workstationEvent = msg.WorkstationEvent;
            var we = _eventSaver.GetWorkstationEvent();
            we.Version = WorkstationEvent.ClassVersion;
            we.Id = workstationEvent.Id;
            we.Date = workstationEvent.Date;
            we.TimeZone = workstationEvent.TimeZone;
            we.EventId = (WorkstationEventType)workstationEvent.EventId;
            we.Severity = (WorkstationEventSeverity)workstationEvent.Severity;
            we.Note = workstationEvent.Note;
            we.DeviceId = workstationEvent.DeviceId;
            we.AccountName = workstationEvent.AccountName;
            we.AccountLogin = workstationEvent.AccountLogin;
            await _eventSaver.AddNewAsync(we);
        }
    }
}
