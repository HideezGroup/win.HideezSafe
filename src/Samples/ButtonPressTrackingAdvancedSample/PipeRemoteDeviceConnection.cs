﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device.Exchange;
using Hideez.SDK.Communication.Interfaces;
using HideezMiddleware.IPC.IncommingMessages.RemoteDevice;
using HideezMiddleware.IPC.Messages.RemoteDevice;
using Meta.Lib.Modules.PubSub;
using System;
using System.Threading.Tasks;

namespace ButtonPressTrackingSample
{
    public class PipeRemoteDeviceConnection : IConnectionController
    {
        private readonly IMetaPubSub _metaPubSub;

        public string Id { get; }

        public string Name { get; }

        /// <summary>
        /// Currently not implemented. Always returns <see cref="ConnectionState.Connected"/>. 
        /// <para>
        /// Use <see cref="DevicesCollectionChangedMessage"/> and <see cref="DeviceConnectionStateChangedMessage"/> to track device state instead.
        /// </para>
        /// </summary>
        public ConnectionState State { get => ConnectionState.Connected; }

        public string Mac { get; }

        public IConnection Connection => throw new NotImplementedException();

        public event EventHandler<MessageBuffer> ResponseReceived;
        public event EventHandler OperationCancelled;
        public event EventHandler<byte[]> DeviceStateChanged;
        public event EventHandler DeviceIsBusy;
        public event EventHandler<FwWipeStatus> WipeFinished;
        public event EventHandler ConnectionStateChanged;
        public event EventHandler<byte[]> FingerprintStateChanged; // Not implemented on the service side


        public PipeRemoteDeviceConnection(IMetaPubSub metaPubSub, string id, string mac, string name)
        {
            _metaPubSub = metaPubSub;

            Id = id;
            Name = name;
            Mac = mac;

            _metaPubSub.TrySubscribeOnServer<RemoteConnection_DeviceStateChangedMessage>(OnDeviceStateChanged);
            _metaPubSub.TrySubscribeOnServer<RemoteConnection_OperationCancelledMessage>(OnOperationCancelled);
            _metaPubSub.TrySubscribeOnServer<RemoteConnection_DeviceIsBusyMessage>(OnDeviceIsBusy);
            _metaPubSub.TrySubscribeOnServer<RemoteConnection_WipeFinishedMessage>(OnWipeFinished);
        }

        private Task OnDeviceStateChanged(RemoteConnection_DeviceStateChangedMessage msg)
        {
            DeviceStateChanged?.Invoke(this, msg.State);
            return Task.CompletedTask;
        }

        private Task OnOperationCancelled(RemoteConnection_OperationCancelledMessage msg)
        {
            OperationCancelled?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        private Task OnDeviceIsBusy(RemoteConnection_DeviceIsBusyMessage msg)
        {
            DeviceIsBusy?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        private Task OnWipeFinished(RemoteConnection_WipeFinishedMessage msg)
        {
            WipeFinished?.Invoke(this, msg.WipeStatus);
            return Task.CompletedTask;
        }

        public bool IsBoot()
        {
            return false;
        }

        public async Task SendRequestAsync(EncryptedRequest request)
        {
            var response = await _metaPubSub.ProcessOnServer<RemoteConnection_RemoteCommandMessageReply>(new RemoteConnection_RemoteCommandMessage(Id, request));
            if (response != null)
            {
                ResponseReceived?.Invoke(this, new MessageBuffer(response.Data, request.Buffer.ChannelNo));
            }
        }

        public async Task SendRequestAsync(ControlRequest request)
        {
            await _metaPubSub.PublishOnServer(new RemoteConnection_ControlRemoteCommandMessage(Id, request));
        }
    }
}
