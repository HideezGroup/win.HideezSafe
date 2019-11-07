﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.ScreenActivation;
using HideezMiddleware.Settings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection
{
    public class RfidConnectionProcessor : Logger, IDisposable
    {
        readonly ConnectionFlowProcessor _connectionFlowProcessor;
        readonly IClientUiManager _clientUiManager;
        readonly HesAppConnection _hesConnection;
        readonly RfidServiceConnection _rfidService;
        readonly ISettingsManager<RfidSettings> _rfidSettingsManager;
        readonly IScreenActivator _screenActivator;
        readonly object _lock = new object();

        int _isConnecting = 0;
        bool isRunning = false;

        public event EventHandler<WorkstationUnlockResult> WorkstationUnlockPerformed;

        public RfidConnectionProcessor(
            ConnectionFlowProcessor connectionFlowProcessor, 
            HesAppConnection hesConnection,
            RfidServiceConnection rfidService, 
            ISettingsManager<RfidSettings> rfidSettingsManager,
            IScreenActivator screenActivator,
            IClientUiManager clientUiManager, 
            ILog log) 
            : base(nameof(RfidConnectionProcessor), log)
        {
            _connectionFlowProcessor = connectionFlowProcessor ?? throw new ArgumentNullException(nameof(connectionFlowProcessor));
            _hesConnection = hesConnection ?? throw new ArgumentNullException(nameof(hesConnection));
            _rfidService = rfidService ?? throw new ArgumentNullException(nameof(rfidService));
            _rfidSettingsManager = rfidSettingsManager ?? throw new ArgumentNullException(nameof(rfidSettingsManager));
            _clientUiManager = clientUiManager ?? throw new ArgumentNullException(nameof(clientUiManager));
            _screenActivator = screenActivator;
        }

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                _rfidService.RfidReceivedEvent -= RfidService_RfidReceivedEvent;
            }

            disposed = true;
        }

        ~RfidConnectionProcessor()
        {
            Dispose(false);
        }
        #endregion

        public void Start()
        {
            lock (_lock)
            {
                if (!isRunning)
                {
                    _rfidService.RfidReceivedEvent += RfidService_RfidReceivedEvent;
                    isRunning = true;
                    WriteLine("Started");
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                isRunning = false;
                _rfidService.RfidReceivedEvent -= RfidService_RfidReceivedEvent;
                WriteLine("Stopped");
            }
        }

        async void RfidService_RfidReceivedEvent(object sender, RfidReceivedEventArgs e)
        {
            await UnlockByRfid(e.Rfid);
        }

        async Task UnlockByRfid(string rfid)
        {
            if (!isRunning)
                return;

            if (!_rfidSettingsManager.Settings.IsRfidEnabled)
                return;

            if (Interlocked.CompareExchange(ref _isConnecting, 1, 1) == 1)
                return;

            try
            {
                _screenActivator?.ActivateScreen();
                await _clientUiManager.SendNotification("Connecting to the HES server...");

                if (_hesConnection == null)
                    throw new Exception("Cannot connect device. Not connected to the HES.");

                // get MAC address from the HES
                var info = await _hesConnection.GetInfoByRfid(rfid);

                if (info == null)
                    throw new Exception($"Device not found");

                if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
                {
                    try
                    {
                        await _connectionFlowProcessor.ConnectAndUnlock(info.DeviceMac, OnUnlockAttempt);
                    }
                    catch (Exception)
                    {
                        // Silent handling. Log is already printed inside of _connectionFlowProcessor.ConnectAndUnlock()
                    }
                    finally
                    {
                        // this delay allows a user to move away the device from the rfid
                        // and prevents the repeated call of this method
                        await Task.Delay(SdkConfig.DelayAfterMainWorkflow);

                        Interlocked.Exchange(ref _isConnecting, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
                await _clientUiManager.SendNotification("");
                await _clientUiManager.SendError(ex.Message);
            }
        }

        void OnUnlockAttempt(WorkstationUnlockResult result)
        {
            if (result.IsSuccessful)
                WorkstationUnlockPerformed?.Invoke(this, result);
        }
    }
}
