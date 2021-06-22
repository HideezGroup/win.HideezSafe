using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using HideezMiddleware.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Settings
{
    [Serializable]
    public class ProximitySettings : BaseSettings
    {
        /// <summary>
        /// Initializes new instance of <see cref="ProximitySettings"/> with default values
        /// </summary>
        public ProximitySettings()
        {
            SettingsVersion = new Version(2, 0);
            DevicesProximity = Array.Empty<DeviceProximitySettings>();
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="copy">Intance to copy from</param>
        public ProximitySettings(ProximitySettings copy)
        {
            if (copy == null)
                return;

            SettingsVersion = (Version)copy.SettingsVersion.Clone();

            var deviceUnlockerSettings = new List<DeviceProximitySettings>(copy.DevicesProximity.Length);

            foreach (var settings in copy.DevicesProximity)
            {
                deviceUnlockerSettings.Add(new DeviceProximitySettings
                {
                    SerialNo = settings.SerialNo,
                    Mac = settings.Mac,
                });
            }

            DevicesProximity = deviceUnlockerSettings.ToArray();
        }

        [Setting]
        public Version SettingsVersion { get; }

        [Setting]
        public DeviceProximitySettings[] DevicesProximity { get; set; }


        /// <summary>
        /// Return proximity settings for device, if not found settings return default settings
        /// </summary>
        public DeviceProximitySettings GetProximitySettings(IDevice device)
        {
            return GetProximitySettings(device.Mac);
        }

        /// <summary>
        /// Return proximity settings for device, if not found settings return default settings
        /// </summary>
        public DeviceProximitySettings GetProximitySettings(string mac)
        {
            var deviceProximity = DevicesProximity.FirstOrDefault(s => s.Mac == mac);
            if (deviceProximity == null)
            {
                deviceProximity = DeviceProximitySettings.DefaultSettings;
                deviceProximity.Mac = mac;
            }
            return deviceProximity;
        }

        public override object Clone()
        {
            return new ProximitySettings(this);
        }
    }
}
