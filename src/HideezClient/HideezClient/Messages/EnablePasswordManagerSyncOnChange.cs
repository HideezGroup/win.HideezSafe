using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages
{
    internal sealed class EnablePasswordManagerSyncOnChange : PubSubMessageBase
    {
        public string DeviceId { get; }

        public EnablePasswordManagerSyncOnChange(string deviceId)
        {
            DeviceId = deviceId;
        }
    }
}
