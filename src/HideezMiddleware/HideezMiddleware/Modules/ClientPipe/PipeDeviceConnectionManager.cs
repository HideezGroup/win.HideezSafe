﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.PipeDevice;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.Modules.DeviceManagement.Messages;
using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.ClientPipe
{
    /// <summary>
    /// Class to manage pipe device connection.
    /// </summary>
    public class PipeDeviceConnectionManager : Logger
    {
        readonly Dictionary<string, PipeDeviceConnectionHandler> pipeDevicesPubSubHandlers = new Dictionary<string, PipeDeviceConnectionHandler>();
        readonly DeviceManager _deviceManager;
        readonly IMetaPubSub _messenger;

        public PipeDeviceConnectionManager(DeviceManager deviceManager, IMetaPubSub pubSub, ILog log) :
            base(nameof(PipeDeviceConnectionManager), log)
        {
            _deviceManager = deviceManager;
            _messenger = pubSub;

            _messenger.Subscribe<EstablishRemoteDeviceConnectionMessage>(EstablishRemoteDeviceConnection);
            _messenger.Subscribe<DeviceManager_DeviceRemovedMessage>(DeviceRemoved);
        }

        private async Task EstablishRemoteDeviceConnection(EstablishRemoteDeviceConnectionMessage args)
        {
            try
            {
                var pipeDevice = (PipeRemoteDeviceProxy)FindDeviceByConnectionId(args.ConnectionId, args.ChannelNo);

                if (pipeDevice == null)
                {
                    var device = FindDeviceByConnectionId(args.ConnectionId, 1) as Device;

                    if (device == null)
                        throw new HideezException(HideezErrorCode.DeviceNotFound, args.ConnectionId);

                    pipeDevice = new PipeRemoteDeviceProxy(device, _log);
                    pipeDevice = (PipeRemoteDeviceProxy)_deviceManager.AddDeviceChannelProxy(pipeDevice, device, args.ChannelNo);

                    PipeDeviceConnectionHandler remoteDevicePubSub = new PipeDeviceConnectionHandler(pipeDevice, _deviceManager, _log);

                    pipeDevicesPubSubHandlers.Add(pipeDevice.Id, remoteDevicePubSub);

                    await _messenger.Publish(new EstablishRemoteDeviceConnectionMessageReply(pipeDevice.Id, remoteDevicePubSub.PipeName, pipeDevice.Name, pipeDevice.Mac));
                }
            }
            catch (Exception ex)
            {
                _log?.WriteLine("", ex);
                throw;
            }
        }

        // Remove pipe connection, if that pipe's device is removed from manager
        private Task DeviceRemoved(DeviceManager_DeviceRemovedMessage msg)
        {
            if (msg.Device is PipeRemoteDeviceProxy pipeDevice)
            {
                pipeDevicesPubSubHandlers.TryGetValue(pipeDevice.Id, out PipeDeviceConnectionHandler remoteDevicePubSubManager);

                remoteDevicePubSubManager.DisposePair();

                pipeDevicesPubSubHandlers.Remove(pipeDevice.Id);
            }

            return Task.CompletedTask;
        }

        IDevice FindDeviceByConnectionId(string connectionId, byte channelNo)
        {
            return _deviceManager.Devices.FirstOrDefault(d => d.DeviceConnection.Connection.ConnectionId.Id == connectionId && d.ChannelNo == channelNo);
        }
    }
}
