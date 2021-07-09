using Meta.Lib.Modules.PubSub;

namespace Hideez.Integration.Lite.Messages
{
    /// <summary>
    /// Sent by device with a set frequency (twice per second) 
    /// and immediately after device button event is fired.
    /// </summary>
    public sealed class DeviceStateChangedMessage : PubSubMessageBase
    {
        /// <summary>
        /// Unique logical device id
        /// </summary>
        public string DeviceId { get; }
        /// <summary>
        /// Approximate device battery percentage
        /// </summary>
        public sbyte Battery { get; }
        /// <summary>
        /// Signal strength between device and connected workstation
        /// </summary>
        public sbyte Rssi { get; }
        /// <summary>
        /// Converted byte value of ButtonPressCode enum
        /// </summary>
        public byte Button { get; }
        /// <summary>
        /// Count of other workstations this device is connected to
        /// </summary>
        public sbyte OtherConnections { get; }

        public DeviceStateChangedMessage(
            string deviceId,
            sbyte battery,
            sbyte rssi,
            byte button,
            sbyte otherConnections)
        {
            DeviceId = deviceId;
            Battery = battery;
            Rssi = rssi;
            Button = button;
            OtherConnections = otherConnections;
        }
    }
}
