using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.Modules.FwUpdateCheck.Messages
{
    public class AvailableDeviceModelsMessage : PubSubMessageBase
    {
        public DeviceModelInfo[] AvailableModels { get; }

        public AvailableDeviceModelsMessage(DeviceModelInfo[] availableModels)
        {
            AvailableModels = availableModels;
        }
    }
}
