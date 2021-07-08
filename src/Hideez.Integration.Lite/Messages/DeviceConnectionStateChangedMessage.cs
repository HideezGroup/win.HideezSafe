using Hideez.Integration.Lite.DTO;
using Meta.Lib.Modules.PubSub;

namespace Hideez.Integration.Lite.Messages
{
    public sealed class DeviceConnectionStateChangedMessage : PubSubMessageBase
    {
        public DeviceDTO Device { get; }

        public DeviceConnectionStateChangedMessage(DeviceDTO device)
        {
            Device = device;
        }
    }
}
