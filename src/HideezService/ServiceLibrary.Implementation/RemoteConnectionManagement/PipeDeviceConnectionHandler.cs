﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.PipeDevice;
using HideezMiddleware;
using HideezMiddleware.IPC.IncommingMessages.RemoteDevice;
using HideezMiddleware.IPC.Messages;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;


namespace ServiceLibrary.Implementation.RemoteConnectionManagement
{
    class PipeDeviceConnectionHandler: Logger
    {
        readonly IMetaPubSub _messenger;
        readonly PipeRemoteDeviceProxy _pipeDevice;
        readonly DeviceManager _deviceManager;

        /// <summary>
        /// Direct MetaPubSub for each pipe device.
        /// </summary>
        readonly IMetaPubSub _remoteConnectionPubSub;

        public string PipeName { get; }

        public PipeDeviceConnectionHandler(IMetaPubSub messenger, PipeRemoteDeviceProxy pipeDevice, DeviceManager deviceManager, ILog log)
            : base(nameof(PipeDeviceConnectionHandler), log)
        {
            PipeName = "HideezRemoteDevicePipe_" + Guid.NewGuid().ToString();

            _remoteConnectionPubSub = new MetaPubSub(new MetaPubSubLogger(new NLogWrapper()));

            _messenger = messenger;
            _pipeDevice = pipeDevice;
            _deviceManager = deviceManager;

            _pipeDevice.DeviceConnection.DeviceStateChanged += RemoteConnection_DeviceStateChanged;

            InitializePubSub();
        }

        void InitializePubSub()
        {
            _remoteConnectionPubSub.StartServer(PipeName, () =>
            {
                var pipeSecurity = new PipeSecurity();
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow));

                var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 32,
                    PipeTransmissionMode.Message, PipeOptions.Asynchronous, 4096, 4096, pipeSecurity);

                return pipe;
            });

            _remoteConnectionPubSub.Subscribe<RemoteClientDisconnectedEvent>(OnRemoteClientDisconnected);
            _remoteConnectionPubSub.Subscribe<RemoteConnection_RemoteCommandMessage>(RemoteConnection_RemoteCommandAsync);
            _remoteConnectionPubSub.Subscribe<RemoteConnection_ControlRemoteCommandMessage>(RemoteConnection_ControlRemoteCommandAsync);
            _remoteConnectionPubSub.Subscribe<RemoteConnection_GetRootKeyMessage>(RemoteConnection_GetRootKeyAsync);
            _remoteConnectionPubSub.Subscribe<RemoteConnection_VerifyCommandMessage>(RemoteConnection_VerifyCommandAsync);
            _remoteConnectionPubSub.Subscribe<RemoteConnection_ResetChannelMessage>(RemoteConnection_ResetChannelAsync);
        }

        #region Event Handlers
        async void RemoteConnection_DeviceStateChanged(object sender, byte[] e)
        {
            try
            {
                if (sender is IConnectionController connection)
                {
                    await _remoteConnectionPubSub.Publish(new RemoteConnection_DeviceStateChangedMessage(connection.Id, e));
                }
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        async Task RemoteConnection_ControlRemoteCommandAsync(RemoteConnection_ControlRemoteCommandMessage arg)
        {
            try
            {
                if (_pipeDevice == null || _pipeDevice.Id != arg.ConnectionId)
                    throw new HideezException(HideezErrorCode.RemoteDeviceNotFound, arg.ConnectionId);

                await _pipeDevice.SendControlRequest(arg.ControlRequest);
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
            }
        }

        async Task RemoteConnection_RemoteCommandAsync(RemoteConnection_RemoteCommandMessage args)
        {
            try
            {
                if (_pipeDevice == null || _pipeDevice.Id != args.ConnectionId)
                    throw new HideezException(HideezErrorCode.RemoteDeviceNotFound, args.ConnectionId);

                var response = await _pipeDevice.SendRequestAndWaitResponseAsync(args.EncryptedRequest, null);

                if (response != null)
                    await _remoteConnectionPubSub.Publish(new RemoteConnection_RemoteCommandMessageReply(response));
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
            }
        }

        async Task RemoteConnection_VerifyCommandAsync(RemoteConnection_VerifyCommandMessage args)
        {
            try
            {
                if (_pipeDevice == null || _pipeDevice.Id != args.ConnectionId)
                    throw new HideezException(HideezErrorCode.RemoteDeviceNotFound, args.ConnectionId);

                if (args.VerifyChannelNo < 2 || args.VerifyChannelNo > 7)
                    throw new HideezException(HideezErrorCode.InvalidChannelNo, args.VerifyChannelNo);

                var response = await _pipeDevice.Device.VerifyEncryption(args.PubKeyH, args.NonceH, args.VerifyChannelNo);

                if (response != null)
                    await _remoteConnectionPubSub.Publish(new RemoteConnection_DeviceCommandMessageReply(response));
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
            }
        }

        async Task RemoteConnection_ResetChannelAsync(RemoteConnection_ResetChannelMessage args)
        {
            try
            {
                if (_pipeDevice == null || _pipeDevice.Id != args.ConnectionId)
                    throw new HideezException(HideezErrorCode.RemoteDeviceNotFound, args.ConnectionId);

                await _pipeDevice.Device.ResetEncryption(args.ChannelNo);
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
            }
        }

        async Task RemoteConnection_GetRootKeyAsync(RemoteConnection_GetRootKeyMessage args)
        {
            try
            {
                if (_pipeDevice == null || _pipeDevice.Id != args.ConnectionId)
                    throw new HideezException(HideezErrorCode.RemoteDeviceNotFound, args.ConnectionId);

                var response = await _pipeDevice.Device.GetRootKey();

                if (response != null)
                    await _remoteConnectionPubSub.Publish(new RemoteConnection_DeviceCommandMessageReply(response));
            }
            catch (Exception ex)
            {
                Error(ex);
                throw;
            }
        }

        Task OnRemoteClientDisconnected(RemoteClientDisconnectedEvent arg)
        {
            DisposePair();

            _deviceManager.RemoveDeviceChannel(_pipeDevice);

            return Task.CompletedTask;
        }
        #endregion

        public void DisposePair()
        {
            _pipeDevice.DeviceConnection.DeviceStateChanged -= RemoteConnection_DeviceStateChanged;

            _remoteConnectionPubSub.StopServer();
        }

        void Error(Exception ex, string message = "")
        {
            _log?.WriteLine(message, ex);
        }
    }
}