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
        readonly Func<AdvertismentReceivedEventArgs, bool> _func;

        public WaitAdvertisementProc(IBleConnectionManager connectionManager)
        {
            _bleConnectionManager = connectionManager;
        }

        public WaitAdvertisementProc(IBleConnectionManager connectionManager, Func<AdvertismentReceivedEventArgs, bool> func)
        {
            _bleConnectionManager = connectionManager;
            _func = func;
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
            if (_func != null)
            {
                if (_func.Invoke(e))
                    _tcs.TrySetResult(e);
            }
            else
                _tcs.TrySetResult(e);
        }
    }
}
