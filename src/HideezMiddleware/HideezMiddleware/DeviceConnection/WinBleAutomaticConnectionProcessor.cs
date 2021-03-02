﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.CredentialProvider;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.Localize;
using HideezMiddleware.Settings;
using HideezMiddleware.Tasks;
using HideezMiddleware.Utils.WorkstationHelper;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinBle;

namespace HideezMiddleware.DeviceConnection
{
    public sealed class WinBleAutomaticConnectionProcessor : BaseConnectionProcessor, IDisposable
    {
        readonly WinBleConnectionManager _winBleConnectionManager;
        readonly WinBleConnectionManagerWrapper _winBleConnectionManagerWrapper;
        readonly ISettingsManager<WorkstationSettings> _workstationSettingsManager;
        readonly AdvertisementIgnoreList _advIgnoreListMonitor;
        readonly DeviceManager _deviceManager;
        readonly CredentialProviderProxy _credentialProviderProxy;
        readonly IClientUiManager _ui;
        readonly IWorkstationHelper _workstationHelper;
        readonly IMetaPubSub _messenger;
        readonly object _lock = new object();
        int _commandLinkInterlock = 0;

        int _isConnecting = 0;
        bool isRunning = false;

        public WinBleAutomaticConnectionProcessor(
            ConnectionFlowProcessor connectionFlowProcessor,
            WinBleConnectionManager winBleConnectionManager,
            WinBleConnectionManagerWrapper winBleConnectionManagerWrapper,
            AdvertisementIgnoreList advIgnoreListMonitor,
            ISettingsManager<WorkstationSettings> workstationSettingsManager,
            DeviceManager deviceManager,
            CredentialProviderProxy credentialProviderProxy,
            IClientUiManager ui,
            IWorkstationHelper workstationHelper,
            IMetaPubSub messenger,
            ILog log)
            : base(connectionFlowProcessor, nameof(ProximityConnectionProcessor), log)
        {
            _winBleConnectionManager = winBleConnectionManager ?? throw new ArgumentNullException(nameof(winBleConnectionManager));
            _winBleConnectionManagerWrapper = winBleConnectionManagerWrapper ?? throw new ArgumentNullException(nameof(winBleConnectionManagerWrapper));
            _workstationSettingsManager = workstationSettingsManager ?? throw new ArgumentNullException(nameof(workstationSettingsManager));
            _advIgnoreListMonitor = advIgnoreListMonitor ?? throw new ArgumentNullException(nameof(advIgnoreListMonitor));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _credentialProviderProxy = credentialProviderProxy ?? throw new ArgumentNullException(nameof(credentialProviderProxy));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
            _workstationHelper = workstationHelper ?? throw new ArgumentNullException(nameof(workstationHelper));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(_messenger));
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
                _winBleConnectionManagerWrapper.AdvertismentReceived -= BleConnectionManager_AdvertismentReceived;
                _winBleConnectionManager.ControllerRemoved -= BleConnectionManager_ControllerRemoved;
                _credentialProviderProxy.CommandLinkPressed -= CredentialProviderProxy_CommandLinkPressed;
                SessionSwitchMonitor.SessionSwitch -= SessionSwitchMonitor_SessionSwitch;
                _messenger.Unsubscribe<ConnectPairedVaultsMessage>(OnConnectPairedVaults);
            }

            disposed = true;
        }

        ~WinBleAutomaticConnectionProcessor()
        {
            Dispose(false);
        }
        #endregion

        public override void Start()
        {
            lock (_lock)
            {
                if (!isRunning)
                {
                    _winBleConnectionManagerWrapper.AdvertismentReceived += BleConnectionManager_AdvertismentReceived;
                    _winBleConnectionManager.ControllerRemoved += BleConnectionManager_ControllerRemoved;
                    _credentialProviderProxy.CommandLinkPressed += CredentialProviderProxy_CommandLinkPressed;
                    SessionSwitchMonitor.SessionSwitch += SessionSwitchMonitor_SessionSwitch;
                    _messenger.Subscribe<ConnectPairedVaultsMessage>(OnConnectPairedVaults);
                    isRunning = true;
                    WriteLine("Started");
                }
            }
        }

        private async Task OnConnectPairedVaults(ConnectPairedVaultsMessage arg)
        {
            WriteLine("Connect paired vaults request");
            _advIgnoreListMonitor.Clear();
            await WaitAdvertisementAndConnectByProximity();
        }

        private void SessionSwitchMonitor_SessionSwitch(int sessionId, Microsoft.Win32.SessionSwitchReason reason)
        {
            switch (reason)
            {
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.SessionUnlock:
                    _advIgnoreListMonitor.Clear();
                    break;
                default:
                    break;
            }
        }

        public override void Stop()
        {
            lock (_lock)
            {
                isRunning = false;
                _winBleConnectionManagerWrapper.AdvertismentReceived -= BleConnectionManager_AdvertismentReceived;
                _winBleConnectionManager.ControllerRemoved -= BleConnectionManager_ControllerRemoved;
                _credentialProviderProxy.CommandLinkPressed -= CredentialProviderProxy_CommandLinkPressed;
                SessionSwitchMonitor.SessionSwitch -= SessionSwitchMonitor_SessionSwitch;
                _messenger.Unsubscribe<ConnectPairedVaultsMessage>(OnConnectPairedVaults);
                WriteLine("Stopped");
            }
        }

        private async void CredentialProviderProxy_CommandLinkPressed(object sender, EventArgs e)
        {
            WriteLine("Command link request");
            _advIgnoreListMonitor.Clear();
            await WaitAdvertisementAndConnectByProximity();
        }

        private void BleConnectionManager_ControllerRemoved(object sender, ControllerRemovedEventArgs e)
        {
            _advIgnoreListMonitor.Remove(e.Controller.Id);
        }

        async void BleConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            await ConnectByProximity(e);
        }

        async Task WaitAdvertisementAndConnectByProximity()
        {
            // Interlock prevents start of multiple or subsequent procedures if impatient user clicks commandLink multiple times
            if (Interlocked.CompareExchange(ref _commandLinkInterlock, 1, 0) == 0)
            {
                try
                {
                    var notifId = nameof(WinBleAutomaticConnectionProcessor);

                    await _ui.SendError("", notifId);
                    await _ui.SendNotification(TranslationSource.Instance["ConnectionProcessor.SearchingForVault"], notifId);
                    var adv = await new WaitAdvertisementProc(_winBleConnectionManagerWrapper).Run(10_000);
                    if (adv != null)
                    {
                        await _ui.SendNotification("", notifId);
                        await ConnectByProximity(adv, true);
                    }
                    else
                    {
                        await _ui.SendNotification("", notifId);
                        await _ui.SendError(TranslationSource.Instance["ConnectionProcessor.VaultNotFound"], notifId);
                    }
                }
                catch (Exception ex)
                {
                    WriteLine(ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref _commandLinkInterlock, 0);
                }
            }
        }

        async Task ConnectByProximity(AdvertismentReceivedEventArgs adv, bool isCommandLinkPressed = false)
        {
            if (!isRunning)
                return;

            if (adv == null)
                return;

            if (_isConnecting == 1)
                return;

            var proximity = BleUtils.RssiToProximity(adv.Rssi);
            var settings = _workstationSettingsManager.Settings;

            if (_workstationHelper.IsActiveSessionLocked())
            {
                if (proximity < settings.UnlockProximity)
                {
                    if (isCommandLinkPressed)
                    {
                        await _ui.SendNotification("");
                        await _ui.SendError(TranslationSource.Instance["ConnectionProcessor.LowProximity"]);
                    }

                    return;
                }
            }

            await ConnectById(adv.Id);
        }

        async Task ConnectById(string id)
        {
            if (_advIgnoreListMonitor.IsIgnored(id))
                return;

            if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
            {
                try
                {
                    try
                    {
                        // If device from advertisement already exists and is connected, ignore advertisement
                        var device = _deviceManager.Devices.FirstOrDefault(d => d.DeviceConnection.Connection.ConnectionId.Id == id && !(d is IRemoteDeviceProxy));
                        if (device != null && device.IsConnected)
                            return;

                        var connectionId = new ConnectionId(id, (byte)DefaultConnectionIdProvider.WinBle);
                        await ConnectAndUnlockByConnectionId(connectionId);
                    }
                    catch (Exception)
                    {
                        // Silent handling. Log is already printed inside of _connectionFlowProcessor.ConnectAndUnlock()
                        // In case of an error, wait a few seconds, before retrying connection
                        var nextConnectionAttemptDelay = 3_000;
                        await Task.Delay(nextConnectionAttemptDelay);
                    }
                    finally
                    {
                        _advIgnoreListMonitor.Ignore(id);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _isConnecting, 0);
                }
            }
        }
    }
}
