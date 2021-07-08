using Hideez.Integration.Lite.Enums;
using Meta.Lib.Modules.PubSub;

namespace Hideez.Integration.Lite.Messages
{
    public sealed class DeviceStateChangedMessage : PubSubMessageBase
    {
        public string DeviceId { get; }
        public sbyte Battery { get; }
        public sbyte Rssi { get; }
        public ButtonPressCode Button { get; }
        public byte RawButton { get; }
        public sbyte OtherConnections { get; }

        public DeviceStateChangedMessage(
            string deviceId,
            sbyte battery,
            sbyte rssi,
            ButtonPressCode button,
            byte rawButton,
            sbyte otherConnections)
        {
            DeviceId = deviceId;
            Battery = battery;
            Rssi = rssi;
            Button = button;
            RawButton = rawButton;
            OtherConnections = otherConnections;
        }
    }
}
