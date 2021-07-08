using Hideez.Integration.Lite.DTO;
using Meta.Lib.Modules.PubSub;

namespace Hideez.Integration.Lite.Messages
{
    public sealed class DevicesCollectionChangedMessage : PubSubMessageBase
    {
        public DeviceDTO[] Devices { get; }

        public DevicesCollectionChangedMessage(DeviceDTO[] devices)
        {
            Devices = devices;
        }
    }
}
