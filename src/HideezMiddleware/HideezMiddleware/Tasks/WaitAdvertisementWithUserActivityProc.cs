using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Proximity.Interfaces;
using Hideez.SDK.Communication.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Tasks
{
    class WaitAdvertisementWithUserActivityProc
    {
        readonly TaskCompletionSource<AdvertismentReceivedEventArgs> _tcs = new TaskCompletionSource<AdvertismentReceivedEventArgs>();
        readonly IBleConnectionManager _bleConnectionManager;
        readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;

        public WaitAdvertisementWithUserActivityProc(IBleConnectionManager connectionManager, IDeviceProximitySettingsProvider proximitySettingsProvider)
        {
            _bleConnectionManager = connectionManager;
            _proximitySettingsProvider = proximitySettingsProvider;
        }

        public async Task<AdvertismentReceivedEventArgs> Run(int timeout)
        {
            try
            {
                _bleConnectionManager.AdvertismentReceived += WinBleConnectionManager_AdvertismentReceived;

                var res = await _tcs.Task.TimeoutAfter(timeout);

                return res;
            }
            catch (TimeoutException)
            {
                return null;
            }
            finally
            {
                _bleConnectionManager.AdvertismentReceived -= WinBleConnectionManager_AdvertismentReceived;
            }
        }

        private void WinBleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs adv)
        {
            var connectionId = new ConnectionId(adv.Id, _bleConnectionManager.Id);
            if (_proximitySettingsProvider.IsEnabledUnlockByActivity(connectionId))
                _tcs.TrySetResult(adv);
        }
    }
}
