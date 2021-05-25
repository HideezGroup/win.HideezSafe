using System;
using System.Threading;
using System.Threading.Tasks;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Connection;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.DeviceConnection.ConnectionProcessors.Dongle
{
    public sealed class TapConnectionProcessor : BaseConnectionProcessor
    {
        readonly IBleConnectionManager _bleConnectionManager;
        readonly object _lock = new object();

        int _isConnecting = 0;
        bool isRunning = false;

        public TapConnectionProcessor(
            ConnectionFlowProcessorBase connectionFlowProcessor,
            IBleConnectionManager bleConnectionManager,
            IMetaPubSub messenger,
            ILog log) 
            : base(connectionFlowProcessor, SessionSwitchSubject.Dongle, nameof(TapConnectionProcessor), messenger, log)
        {
            _bleConnectionManager = bleConnectionManager ?? throw new ArgumentNullException(nameof(bleConnectionManager));
        }

        public override void Start()
        {
            lock (_lock)
            {
                if (!isRunning)
                {
                    _bleConnectionManager.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
                    isRunning = true;
                    WriteLine("Started");
                }
            }
        }

        public override void Stop()
        {
            lock (_lock)
            {
                isRunning = false;
                _bleConnectionManager.AdvertismentReceived -= BleConnectionManager_AdvertismentReceived;
                WriteLine("Stopped");
            }
        }

        async void BleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            await UnlockByTap(e);
        }

        async Task UnlockByTap(AdvertismentReceivedEventArgs adv)
        {
            // Standard Checks
            if (!isRunning)
                return;

            if (adv == null)
                return;

            if (_isConnecting == 1)
                return;

            // Tap related checks
            if (adv.Rssi <= SdkConfig.TapProximityUnlockThreshold)
                return;

            if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
            {
                try
                {
                    var connectionId = new ConnectionId(adv.Id, (byte)DefaultConnectionIdProvider.Csr);
                    await ConnectAndUnlockByConnectionId(connectionId);
                }
                catch (Exception)
                {
                    // Silent handling. Log is already printed inside of _connectionFlowProcessor.ConnectAndUnlock()
                }
                finally
                {
                    // this delay allows a user to move away the device from the dongle
                    // and prevents the repeated call of this method
                    await Task.Delay(SdkConfig.DelayAfterMainWorkflow);

                    Interlocked.Exchange(ref _isConnecting, 0);
                }
            }
        }
    }
}
