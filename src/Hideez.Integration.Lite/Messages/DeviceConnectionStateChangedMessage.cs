using Hideez.Integration.Lite.DTO;
using Meta.Lib.Modules.PubSub;

namespace Hideez.Integration.Lite.Messages
{
    /// <summary>
    /// Sent when known physical device connection state changes to connected or disconnected,
    /// indicated by <see cref="DeviceDTO.IsConnected"/> property
    /// </summary>
    public sealed class DeviceConnectionStateChangedMessage : PubSubMessageBase
    {
        public DeviceDTO Device { get; }

        public DeviceConnectionStateChangedMessage(DeviceDTO device)
        {
            Device = device;
        }
    }
}
