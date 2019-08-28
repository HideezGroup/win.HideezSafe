﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.Settings;
using HideezMiddleware.Utils;

namespace HideezMiddleware.DeviceConnection
{

    class ProximityConnectionProcessor : BaseConnectionProcessor, IDisposable
    {
        struct ProximityUnlockAccess
        {
            public string Mac { get; set; }
            public bool CanConnect { get; set; }
        }

        readonly IBleConnectionManager _bleConnectionManager;
        readonly IScreenActivator _screenActivator;
        readonly IClientUi _clientUi;
        readonly ISettingsManager<UnlockerSettings> _unlockerSettingsManager;
        readonly AdvertisementIgnoreList _advIgnoreListMonitor;

        List<string> MacListToConnect { get; set; }

        public ProximityConnectionProcessor(
            ConnectionFlowProcessor connectionFlowProcessor,
            IBleConnectionManager bleConnectionManager,
            IScreenActivator screenActivator,
            IClientUi clientUi,
            ISettingsManager<UnlockerSettings> unlockerSettingsManager,
            AdvertisementIgnoreList advIgnoreListMonitor,
            ILog log) 
            : base(connectionFlowProcessor, nameof(ProximityConnectionProcessor), log)
        {
            _bleConnectionManager = bleConnectionManager;
            _screenActivator = screenActivator;
            _clientUi = clientUi;
            _unlockerSettingsManager = unlockerSettingsManager;
            _advIgnoreListMonitor = advIgnoreListMonitor;

            _bleConnectionManager.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
            _unlockerSettingsManager.SettingsChanged += UnlockerSettingsManager_SettingsChanged;

            SetAccessListFromSettings(_unlockerSettingsManager.Settings);
        }

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed = false;
        void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                _bleConnectionManager.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
                _unlockerSettingsManager.SettingsChanged -= UnlockerSettingsManager_SettingsChanged;
            }

            disposed = true;
        }

        ~ProximityConnectionProcessor()
        {
            Dispose(false);
        }
        #endregion

        // Todo: Maybe add Start/Stop methods to TapConnectionProcessor

        void SetAccessListFromSettings(UnlockerSettings settings)
        {
            MacListToConnect = settings.DeviceUnlockerSettings
                .Where(s => s.AllowProximity)
                .Select(s => s.Mac)
                .ToList();
        }

        void UnlockerSettingsManager_SettingsChanged(object sender, SettingsChangedEventArgs<UnlockerSettings> e)
        {
            SetAccessListFromSettings(e.NewSettings);
        }

        async void BleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            await ConnectByProximity(e);
        }

        async Task ConnectByProximity(AdvertismentReceivedEventArgs adv)
        {
            if (MacListToConnect.Count == 0)
                return;

            var mac = MacUtils.GetMacFromShortMac(adv.Id);
            if (!MacListToConnect.Any(m => m == mac))
                return;

            var proximity = BleUtils.RssiToProximity(adv.Rssi);
            if (proximity < _unlockerSettingsManager.Settings.UnlockProximity)
                return;

            if (_advIgnoreListMonitor.IsIgnored(mac))
                return;

            await ConnectDeviceByMac(mac);
        }
    }
}
