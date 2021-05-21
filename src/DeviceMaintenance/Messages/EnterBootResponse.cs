using Hideez.SDK.Communication.FW;
using Meta.Lib.Modules.PubSub;

namespace DeviceMaintenance.Messages
{
    public class EnterBootResponse : PubSubMessageBase
    {
        public FirmwareImageUploader ImageUploader { get; }
        public string DeviceId { get; }

        public EnterBootResponse(FirmwareImageUploader imageUploader, string deviceId)
        {
            ImageUploader = imageUploader;
            DeviceId = deviceId;
        }
    }
}
