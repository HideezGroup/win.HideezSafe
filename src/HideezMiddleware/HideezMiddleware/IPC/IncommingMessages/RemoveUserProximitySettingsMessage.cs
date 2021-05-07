using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.IPC.IncommingMessages
{
    public class RemoveUserProximitySettingsMessage: PubSubMessageBase
    {
        public string DeviceConnectionId { get; set; }

        public RemoveUserProximitySettingsMessage(string connectionId)
        {
            DeviceConnectionId = connectionId;
        }
    }
}
