using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.CredentialProvider;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.Localize;
using HideezMiddleware.Tasks;
using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.DeviceConnection.ConnectionProcessors.WinBle
{
    public sealed class CommandLinkConnectionProcessor : BaseConnectionProcessor
    {
        readonly IBleConnectionManager _bleConnectionManager;
        readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;
        readonly AdvertisementIgnoreList _advIgnoreListMonitor;
        readonly DeviceManager _deviceManager;
        readonly IWorkstationUnlocker _workstationUnlocker;
        readonly IClientUiManager _ui;

        readonly object _lock = new object();
        int _commandLinkInterlock = 0;
        int _isConnecting = 0;
        bool isRunning = false;

        public CommandLinkConnectionProcessor(
            IConnectionFlowProcessor connectionFlowProcessor,
            IBleConnectionManager bleConnectionManager,
            IDeviceProximitySettingsProvider proximitySettingsProvider,
            AdvertisementIgnoreList advIgnoreListMonitor,
            DeviceManager deviceManager,
            IWorkstationUnlocker workstationUnlocker,
            IClientUiManager ui,
            IMetaPubSub messenger,
            ILog log)
            : base(connectionFlowProcessor, SessionSwitchSubject.Proximity, nameof(CommandLinkConnectionProcessor), messenger, log)
        {
            _bleConnectionManager = bleConnectionManager ?? throw new ArgumentNullException(nameof(bleConnectionManager));
            _proximitySettingsProvider = proximitySettingsProvider ?? throw new ArgumentNullException(nameof(_proximitySettingsProvider));
            _advIgnoreListMonitor = advIgnoreListMonitor ?? throw new ArgumentNullException(nameof(advIgnoreListMonitor));
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _workstationUnlocker = workstationUnlocker ?? throw new ArgumentNullException(nameof(workstationUnlocker));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        }

        public override void Start()
        {
            lock (_lock)
            {
                if (!isRunning)
                {
                    _workstationUnlocker.CommandLinkPressed += CredentialProviderProxy_CommandLinkPressed;
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
                _workstationUnlocker.CommandLinkPressed -= CredentialProviderProxy_CommandLinkPressed;
                WriteLine("Stopped");
            }
        }

        private async void CredentialProviderProxy_CommandLinkPressed(object sender, EventArgs e)
        {
            WriteLine("Command link pressed");
            _advIgnoreListMonitor.Clear();
            await WaitAdvertisementAndConnectByActivity();
        }

        async Task WaitAdvertisementAndConnectByActivity()
        {
            // Interlock prevents start of multiple or subsequent procedures if impatient user clicks commandLink multiple times
            if (Interlocked.CompareExchange(ref _commandLinkInterlock, 1, 0) == 0)
            {
                var notifId = nameof(CommandLinkConnectionProcessor);
                try
                {
                    await _ui.SendError("", notifId);
                    await _ui.SendNotification(TranslationSource.Instance["ConnectionProcessor.SearchingForVault"], notifId);
                    var adv = await new WaitAdvertisementProc(_bleConnectionManager).Run(10_000);
                    if (adv != null)
                    {
                        await ConnectByActivity(adv);
                    }
                    else
                    {
                        await _ui.SendError(TranslationSource.Instance["ConnectionProcessor.VaultNotFound"], notifId);
                    }
                }
                catch (Exception ex)
                {
                    WriteLine(ex.Message);
                }
                finally
                {
                    await _ui.SendNotification("", notifId);
                    Interlocked.Exchange(ref _commandLinkInterlock, 0);
                }
            }
        }

        async Task ConnectByActivity(AdvertismentReceivedEventArgs adv)
        {
            // Standard checks
            if (!isRunning)
                return;

            if (adv == null)
                return;

            if (_isConnecting == 1)
                return;

            // Proximity related checks
            var connectionId = new ConnectionId(adv.Id, _bleConnectionManager.Id);
            if (!_proximitySettingsProvider.IsEnabledUnlock(connectionId))
                return;

            var proximity = BleUtils.RssiToProximity(adv.Rssi);
            if (proximity < _proximitySettingsProvider.GetUnlockProximity(connectionId))
            {
                await _ui.SendNotification("");
                await _ui.SendError(TranslationSource.Instance["ConnectionProcessor.LowProximity"]);
                return;
            }

            if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
            {
                try
                {
                    var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == adv.Id && !(d is IRemoteDeviceProxy) && !d.IsBoot);

                    // Unlocked Workstation and device haven't finished main workflow - break and dont add to ignore
                    if (!_workstationUnlocker.IsConnected
                        && device?.GetUserProperty<bool>(WorkflowProperties.HV_FINISHED_WF) != true)
                        return;

                    try
                    {
                        // Unlocked Workstation and device finished main workflow - break and add to ignore
                        if (!_workstationUnlocker.IsConnected
                            && device?.GetUserProperty<bool>(WorkflowProperties.HV_FINISHED_WF) == true)
                            return;

                        // Locked Workstation and haven't finished main workflow - connect add to ignore
                        if (_workstationUnlocker.IsConnected
                            && device?.GetUserProperty<bool>(WorkflowProperties.HV_FINISHED_WF) != true)
                        {
                            await ConnectAndUnlockByConnectionId(connectionId);
                        }
                    }
                    catch (Exception)
                    {
                        // Silent handling. Log is already printed inside of _connectionFlowProcessor.ConnectAndUnlock()
                    }
                    finally
                    {
                        _advIgnoreListMonitor.Ignore(adv.Id);
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
