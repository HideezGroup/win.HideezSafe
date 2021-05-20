using Hideez.SDK.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Settings
{
    public class UserDeviceProximitySettings
    {
        public string Id { get; set; }
        public int LockProximity { get; set; }
        public int UnlockProximity { get; set; }
        public bool EnabledLockByProximity { get; set; }
        public bool EnabledUnlock { get; set; }
        public bool EnabledUnlockByProximity { get; set; }
        public bool DisabledDisplayAuto { get; set; }

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
                    EnabledUnlockByProximity = false,
                    DisabledDisplayAuto = false,
                };
            }
        }
    }
}
