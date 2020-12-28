﻿using Hideez.SDK.Communication.Device;
using Meta.Lib.Modules.PubSub;
using System.Threading.Tasks;

namespace HideezClient.Modules
{
    public interface IRemoteDeviceFactory
    {
        Task<Device> CreateRemoteDeviceAsync(string connectionId, byte channelNo, IMetaPubSub remoteDeviceMessenger);
    }
}