using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Proximity.Interfaces;
using Hideez.SDK.Communication.Utils;
using System;
using System.Linq;
using System.Threading.Tasks;
using WinBle;

namespace HideezMiddleware.Tasks
{
    public sealed class WaitAdvertisementProc
    {
        readonly TaskCompletionSource<AdvertismentReceivedEventArgs> _tcs = new TaskCompletionSource<AdvertismentReceivedEventArgs>();
        readonly IBleConnectionManager _bleConnectionManager;
        private readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;

        public WaitAdvertisementProc(IBleConnectionManager connectionManager)
        {
            _bleConnectionManager = connectionManager;
        }

        public WaitAdvertisementProc(IBleConnectionManager connectionManager, IDeviceProximitySettingsProvider proximitySettingsProvider)
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

        private void WinBleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            if (_proximitySettingsProvider != null)
            {
                var connectionId = new ConnectionId(e.Id, _bleConnectionManager.Id);
                if (_proximitySettingsProvider.GetUnlockProximity(connectionId) >= BleUtils.RssiToProximity(e.Rssi))
                    _tcs.TrySetResult(e);
            }
            else
                _tcs.TrySetResult(e);
        }
    }
}
