﻿using GalaSoft.MvvmLight.Messaging;
using Hideez.SDK.Communication.Remote;
using HideezSafe.Modules.ServiceProxy;
using System.Threading.Tasks;

namespace HideezSafe.Modules
{
    class RemoteDeviceFactory : IRemoteDeviceFactory
    {
        readonly IServiceProxy _serviceProxy;
        readonly IMessenger _messenger;

        public RemoteDeviceFactory(IServiceProxy serviceProxy, IMessenger messenger)
        {
            _serviceProxy = serviceProxy;
            _messenger = messenger;
        }

        public async Task<RemoteDevice> CreateRemoteDevice(string mac, byte channelNo)
        {
            var connectionId = await _serviceProxy.GetService().EstablishRemoteDeviceConnectionAsync(mac, channelNo);

            var remoteConnection = new RemoteDeviceConnection(_serviceProxy, _messenger);

            var device = new RemoteDevice(connectionId, remoteConnection);

            remoteConnection.RemoteDevice = device;

            return device;
        }
    }
}
