using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages
{
    internal sealed class DisablePasswordManagerSyncOnChange : PubSubMessageBase
    {
        public string DeviceId { get; }

        public DisablePasswordManagerSyncOnChange(string deviceId)
        {
            DeviceId = deviceId;
        }
    }
}
