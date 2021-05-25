using Hideez.SDK.Communication;

namespace HideezMiddleware.Settings
{
    public class UserDeviceProximitySettings
    {
        public string Id { get; set; }
        public int LockProximity { get; set; }
        public int UnlockProximity { get; set; }
        public bool EnabledLockByProximity { get; set; }
        public bool EnabledUnlock { get; set; }
        public bool EnabledUnlockByActivation { get; set; }
        public bool EnabledUnlockByProximity { get; set; }

        public static UserDeviceProximitySettings DefaultSettings
        {
            get
            {
                return new UserDeviceProximitySettings()
                {
                    LockProximity = SdkConfig.DefaultLockProximity,
                    UnlockProximity = SdkConfig.DefaultUnlockProximity,
                    EnabledLockByProximity = false,
                    EnabledUnlock = false,
                    EnabledUnlockByActivation = false,
                    EnabledUnlockByProximity = false,
                };
            }
        }
    }
}
