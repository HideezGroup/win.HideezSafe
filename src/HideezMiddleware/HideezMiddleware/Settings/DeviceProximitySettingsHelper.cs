using Hideez.SDK.Communication.HES.Client;
using System.Collections.Generic;
using System.Linq;

namespace HideezMiddleware.Settings
{
    public class DeviceProximitySettingsHelper
    {
        private readonly ISettingsManager<ProximitySettings> proximitySettingsManager;

        public DeviceProximitySettingsHelper(ISettingsManager<ProximitySettings> proximitySettingsManager)
        {
            this.proximitySettingsManager = proximitySettingsManager;
        }

        public bool GetAllowEditProximity(string mac)
        {
            return proximitySettingsManager.Settings.DevicesProximity.FirstOrDefault(s => s.Mac == mac) != null;
        }

        public void SaveOrUpdate(IReadOnlyList<DeviceProximitySettings> dtos)
        {
            var settings = proximitySettingsManager.Settings;
            var devicesProximity = settings.DevicesProximity.ToList();
            devicesProximity.RemoveAll(dp => dtos.FirstOrDefault(dto => dto.Mac == dp.Mac) == null);

            foreach (var dto in dtos)
            {
                Update(devicesProximity, dto);
            }

            settings.DevicesProximity = devicesProximity.ToArray();
            proximitySettingsManager.SaveSettings(settings);
        }

        public void SetClientProximity(string mac, int lockProximity, int unlockProximity)
        {
            var settings = proximitySettingsManager.Settings;
            var deviceProximity = settings.DevicesProximity.FirstOrDefault(s => s.Mac == mac);
            if (deviceProximity != null)
            {
                proximitySettingsManager.SaveSettings(settings);
            }
        }


        private void Update(List<DeviceProximitySettings> settings, DeviceProximitySettings newSettings)
        {
            var deviceProximity = settings.FirstOrDefault(s => s.Mac == newSettings.Mac);
            if (deviceProximity == null)
            {
                deviceProximity = DeviceProximitySettings.DefaultSettings;
                deviceProximity.Mac = newSettings.Mac;
                deviceProximity.SerialNo = newSettings.SerialNo;
                settings.Add(deviceProximity);
            }
        }
    }
}
