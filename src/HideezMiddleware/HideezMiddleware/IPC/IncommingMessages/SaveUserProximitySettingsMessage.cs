using HideezMiddleware.Settings;
using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.IPC.IncommingMessages
{
    public sealed class SaveUserProximitySettingsMessage : PubSubMessageBase
    {
        public UserDeviceProximitySettings UserDeviceProximitySettings { get; set; }

        public SaveUserProximitySettingsMessage(UserDeviceProximitySettings userProximitySettings)
        {
            UserDeviceProximitySettings = userProximitySettings;
        }
    }
}
