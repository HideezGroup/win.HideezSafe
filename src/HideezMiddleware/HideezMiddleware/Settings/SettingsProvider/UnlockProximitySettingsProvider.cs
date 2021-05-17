using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Settings.SettingsProvider
{
    public class UnlockProximitySettingsProvider: Logger, IDeviceProximitySettingsProvider
    {
        readonly ISettingsManager<ProximitySettings> _unlockProximitySettingsManager;

        ProximitySettings _unlockProximitySettings;

        public UnlockProximitySettingsProvider(
            ISettingsManager<ProximitySettings> unlockProximitySettingsManager,
            ILog log) :
            base(nameof(UserProximitySettingsProvider), log)
        {
            _unlockProximitySettingsManager = unlockProximitySettingsManager;

            _unlockProximitySettingsManager.SettingsChanged += DevicesProximitySettingsManager_SettingsChanged;

            LoadSettings();
        }

        private void DevicesProximitySettingsManager_SettingsChanged(object sender, SettingsChangedEventArgs<ProximitySettings> e)
        {
            LoadSettings();
        }

        void LoadSettings()
        {
            try
            {
                _unlockProximitySettings = _unlockProximitySettingsManager.Settings;
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        public int GetLockProximity(ConnectionId connectionId)
        {
            return SdkConfig.DefaultLockProximity;
        }

        public int GetUnlockProximity(ConnectionId connectionId)
        {
            return SdkConfig.DefaultUnlockProximity;
        }

        public int GetLockTimeout(ConnectionId connectionId)
        {
            return SdkConfig.DefaultLockTimeout;
        }

        public int GetProximityTimeout(ConnectionId connectionId)
        {
            return GetLockTimeout(connectionId) * 2;

        }

        public bool IsEnabledLockByProximity(ConnectionId connectionId)
        {
            return true;

        }

        public bool IsEnabledUnlockByProximity(ConnectionId connectionId)
        {
            var isExistSettings = _unlockProximitySettings.DevicesProximity.Any(s => s.Mac == BleUtils.ConnectionIdToMac(connectionId.Id));
            return isExistSettings;
        }

        public bool IsDisabledAutoDisplay(ConnectionId connectionId)
        {
            return false;
        }
    }
}
