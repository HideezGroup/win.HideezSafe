using Hideez.Integration.Lite.DTO;
using Meta.Lib.Modules.PubSub;

namespace Hideez.Integration.Lite.Messages
{
    /// <summary>
    /// Sent when collection of known devices changes as a result of the following:
    /// <para>Previously unlisted device connected</para>
    /// <para>Device removed, usually as result of unpairing</para>
    /// <para>Remote device created</para>
    /// <para>Remote device removed</para>
    /// </summary>
    public sealed class DevicesCollectionChangedMessage : PubSubMessageBase
    {
        public DeviceDTO[] Devices { get; }

        public DevicesCollectionChangedMessage(DeviceDTO[] devices)
        {
            Devices = devices;
        }
    }
}
