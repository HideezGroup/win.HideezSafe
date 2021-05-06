using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages
{
    internal sealed class ForcePasswordManagerSync : PubSubMessageBase
    {
        public string DeviceId { get; }

        public ForcePasswordManagerSync(string deviceId)
        {
            DeviceId = deviceId;
        }
    }
}
