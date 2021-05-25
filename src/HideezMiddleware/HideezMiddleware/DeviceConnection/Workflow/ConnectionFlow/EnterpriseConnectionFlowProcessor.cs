using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using HideezMiddleware.Localize;
using HideezMiddleware.ScreenActivation;
using HideezMiddleware.Settings;
using HideezMiddleware.Tasks;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hideez.SDK.Communication.Interfaces;
using System.Linq;
using Hideez.SDK.Communication.Connection;
using HideezMiddleware.Utils.WorkstationHelper;
using HideezMiddleware.DeviceLogging;
using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow
{
    public sealed class EnterpriseConnectionFlowProcessor : ConnectionFlowProcessorBase
    {
        public struct ConnectionFlowSubprocessorsStruct
        {
            public IPermissionsCheckProcessor PermissionsCheckProcessor;
            public IVaultConnectionProcessor VaultConnectionProcessor;
            public ICacheVaultInfoProcessor CacheVaultInfoProcessor;
            public ILicensingProcessor LicensingProcessor;
            public IStateUpdateProcessor StateUpdateProcessor;
            public IActivationProcessor ActivationProcessor;
            public IVaultAuthorizationProcessor MasterkeyProcessor;
            public IAccountsUpdateProcessor AccountsUpdateProcessor;
            public IUserAuthorizationProcessor UserAuthorizationProcessor;
            public IUnlockProcessor UnlockProcessor;
        }

        readonly DeviceManager _deviceManager;
        readonly IWorkstationUnlocker _workstationUnlocker; // Todo: remove and replace calls with unlockProcessor
        readonly IScreenActivator _screenActivator;
        readonly IClientUiManager _ui;
        readonly IHesAppConnection _hesConnection;
        readonly IHesAccessManager _hesAccessManager;
        readonly ISettingsManager<ServiceSettings> _serviceSettingsManager;
        readonly IWorkstationHelper _workstationHelper;
        readonly DeviceLogManager _deviceLogManager;

        readonly ConnectionFlowSubprocessorsStruct _subp;

        public override event EventHandler<string> Started;
        public override event EventHandler<IDevice> DeviceFinilizingMainFlow;
        public override event EventHandler<IDevice> DeviceFinishedMainFlow;
        public override event EventHandler<string> Finished;
        public override event EventHandler<string> AttemptingUnlock;
        public override event EventHandler<string> UnlockAttempted;

        public EnterpriseConnectionFlowProcessor(
            DeviceManager deviceManager,
            IHesAppConnection hesConnection,
            IWorkstationUnlocker workstationUnlocker,
            IScreenActivator screenActivator,
            IClientUiManager ui,
            IHesAccessManager hesAccessManager,
            ISettingsManager<ServiceSettings> serviceSettingsManager,
            ConnectionFlowSubprocessorsStruct subprocs,
            IWorkstationHelper workstationHelper,
            DeviceLogManager deviceLogManager,
            IMetaPubSub messenger,
            ILog log)
            : base(messenger, nameof(EnterpriseConnectionFlowProcessor), log)
        {
            _deviceManager = deviceManager;
            _workstationUnlocker = workstationUnlocker;
            _screenActivator = screenActivator;
            _ui = ui;
            _hesConnection = hesConnection;
            _hesAccessManager = hesAccessManager;
            _serviceSettingsManager = serviceSettingsManager;

            _subp = subprocs;
            _workstationHelper = workstationHelper;
            _deviceLogManager = deviceLogManager;

            _hesAccessManager.AccessRetracted += HesAccessManager_AccessRetractedEvent;
            _serviceSettingsManager.SettingsChanged += ServiceSettingsManager_SettingsChanged;
        }

        void ServiceSettingsManager_SettingsChanged(object sender, SettingsChangedEventArgs<ServiceSettings> e)
        {
            if (e.NewSettings.AlarmTurnOn)
                Cancel("Alarm enabled on HES");
        }

        void HesAccessManager_AccessRetractedEvent(object sender, EventArgs e)
        {
            // Cancel the workflow if workstation access was retracted on HES
            Cancel("Workstation access retracted");
        }

        protected override async Task MainWorkflow(ConnectionId connectionId, ConnectionFlowOptions options, Action<WorkstationUnlockResult> onUnlockAttempt, CancellationToken ct)
        {
            // Ignore MainFlow requests for devices that are already connected
            // IsConnected-true indicates that device already finished main flow or is in progress
            var existingDevice = _deviceManager.Devices.FirstOrDefault(d => d.DeviceConnection.Connection.ConnectionId == connectionId
                && d.ChannelNo == (int)DefaultDeviceChannel.Main);
            if (existingDevice != null && existingDevice.IsConnected && existingDevice.IsInitialized && !_workstationHelper.IsActiveSessionLocked())
                return;

            WriteLine($"Started main workflow ({connectionId.Id}, {(DefaultConnectionIdProvider)connectionId.IdProvider})");

            var flowId = Guid.NewGuid().ToString();
            IsRunning = true;
            Started?.Invoke(this, flowId);

            bool workflowFinishedSuccessfully = false;
            bool deleteVaultBondOnError = false;
            bool cleanupNotifications = false;
            string errorMessage = null;
            IDevice device = null;

            try
            {
                await _ui.SendError(string.Empty, string.Empty); // Empty Id is intended to be processed only by CredentialProvider
                await _ui.SendNotification(string.Empty, connectionId.Id);

                _subp.PermissionsCheckProcessor.CheckPermissions();

                // Start periodic screen activator to raise the "curtain"
                if (_workstationHelper.IsActiveSessionLocked())
                {
                    _screenActivator?.ActivateScreen();
                    _screenActivator?.StartPeriodicScreenActivation(0);

                    await new WaitWorkstationUnlockerConnectProc(_workstationUnlocker)
                        .Run(SdkConfig.WorkstationUnlockerConnectTimeout, ct);
                }

                if (options.UseReconnectProcedure)
                    device = await _subp.VaultConnectionProcessor.ReconnectVault(connectionId, ct);
                else
                    device = await _subp.VaultConnectionProcessor.ConnectVault(connectionId, options.RebondOnConnectionFail, ct);
                device.Disconnected += OnVaultDisconnectedDuringFlow;
                device.OperationCancelled += OnCancelledByVaultButton;

                await _subp.VaultConnectionProcessor.WaitVaultInitialization(device, ct);

                if (device.IsBoot)
                {
                    throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.Error.VaultInBootloaderMode"]);
                }
                
                if(device.LicenseInfo == 0 && device.AccessLevel.IsLinkRequired == false)
                    throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.Error.VaultInStandaloneMode"]);

                await _deviceLogManager.TryReadDeviceLog(device);

                // Different device with the same name indicates that a single physical device is connected through different channel
                // This temporary fix is applied to prevent this behavior
                // TODO: Another temporary fix is to compare by Id instead of by refence. Should be fixed too
                if (_deviceManager.Devices.FirstOrDefault(d => d.Id != device.Id
                    && !(d is IRemoteDeviceProxy)
                    && d.Name == device.Name
                    && d.IsConnected) != null)
                    throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.Error.VaultAlreadyConnected"]);
                // ...
                
                device.SetUserProperty(CustomProperties.HW_CONNECTION_STATE_PROP, HwVaultConnectionState.Initializing);

                HwVaultInfoFromHesDto vaultInfo = new HwVaultInfoFromHesDto(); // Initializes with default values for when HES is not connected
                if (_hesConnection.State == HesConnectionState.Connected)
                    vaultInfo = await _hesConnection.UpdateHwVaultProperties(new HwVaultInfoFromClientDto(device), true);

                _subp.CacheVaultInfoProcessor.CacheAndUpdateVaultOwner(ref device, vaultInfo, ct);

                await _subp.LicensingProcessor.CheckLicense(device, vaultInfo, ct);
                vaultInfo = await _subp.StateUpdateProcessor.UpdateVaultStatus(device, vaultInfo, ct);
                vaultInfo = await _subp.ActivationProcessor.ActivateVault(device, vaultInfo, ct);

                await _subp.MasterkeyProcessor.AuthVault(device, ct);

                var osAccUpdateTask = _subp.AccountsUpdateProcessor.UpdateAccounts(device, vaultInfo, true);

                if (options.TryUnlock)
                {
                    var sid = _workstationHelper.GetSessionId();
                    var state = _workstationHelper.GetSessionLockState(sid);
                    var name = _workstationHelper.GetSessionName(sid);
                    var isCurrentLocked = _workstationHelper.IsActiveSessionLocked();
                    WriteLine($"Session check for unlock sid:{sid}, state:{state}, name:{name}, islocked: {isCurrentLocked}");
                }
                else
                    WriteLine("Skip unlock step");

                if (_workstationUnlocker.IsConnected && _workstationHelper.IsActiveSessionLocked() && options.TryUnlock)
                {
                    await Task.WhenAll(_subp.UserAuthorizationProcessor.AuthorizeUser(device, ct), osAccUpdateTask);

                    _screenActivator?.StopPeriodicScreenActivation();
                    AttemptingUnlock?.Invoke(this, flowId);
                    await _subp.UnlockProcessor.UnlockWorkstation(device, flowId, onUnlockAttempt, ct);
                }
                else
                    await osAccUpdateTask;

                device.SetUserProperty(CustomProperties.HW_CONNECTION_STATE_PROP, HwVaultConnectionState.Finalizing);
                WriteLine($"Finalizing main workflow: ({device.Id})");
                DeviceFinilizingMainFlow?.Invoke(this, device);

                await _subp.AccountsUpdateProcessor.UpdateAccounts(device, vaultInfo, false);

                device.SetUserProperty(CustomProperties.HW_CONNECTION_STATE_PROP, HwVaultConnectionState.Online);

                if (_hesConnection.State == HesConnectionState.Connected)
                    await _hesConnection.UpdateHwVaultProperties(new HwVaultInfoFromClientDto(device), false);

                workflowFinishedSuccessfully = true;
            }
            catch (HideezException ex)
            {
                switch (ex.ErrorCode)
                {
                    case HideezErrorCode.DeviceIsLocked:
                    case HideezErrorCode.DeviceNotAssignedToUser:
                    case HideezErrorCode.HesDeviceNotFound:
                    case HideezErrorCode.HesDeviceCompromised:
                    case HideezErrorCode.HesDeviceLinkedToAnotherServer:
                        // These errors require bond removal, but only when using CSR
                        if (connectionId.IdProvider == (byte)DefaultConnectionIdProvider.Csr)
                            deleteVaultBondOnError = true;
                        errorMessage = HideezExceptionLocalization.GetErrorAsString(ex);
                        break;
                    case HideezErrorCode.DeviceHasBeenWiped:
                        // This error always requires bond removal
                        deleteVaultBondOnError = true;
                        errorMessage = HideezExceptionLocalization.GetErrorAsString(ex);
                        break;
                    case HideezErrorCode.ButtonConfirmationTimeout:
                    case HideezErrorCode.GetPinTimeout:
                    case HideezErrorCode.GetActivationCodeTimeout:
                        // Silent handling
                        cleanupNotifications = true;
                        WriteLine(ex);
                        break;
                    case HideezErrorCode.HesNotConnected:
                        // We need to display an error message which is different from one that is usually shown for that error code.
                        errorMessage = TranslationSource.Instance["ConnectionFlow.Error.UnexpectedlyLostNetworkConnection"];
                        break;
                    default:
                        errorMessage = HideezExceptionLocalization.GetErrorAsString(ex);
                        break;
                }
            }
            catch (VaultFailedToAuthorizeException ex)
            {
                // User should never receive this error unless there is a bug in algorithm 
                errorMessage = HideezExceptionLocalization.GetErrorAsString(ex);
            }
            catch (WorkstationUnlockFailedException ex)
            {
                // Silent handling of failed workstation unlock
                // The actual message will be displayed by credential provider
                WriteLine(ex);
            }
            catch (OperationCanceledException ex)
            {
                // Silent cancelation handling
                WriteLine(ex);
            }
            catch (TimeoutException ex)
            {
                // Silent timeout handling
                WriteLine(ex);
            }
            catch (WebSocketException ex)
            {
                // Retrieve the most inner WebSocketException to retrieve the error code
                if (ex.WebSocketErrorCode != 0)
                    errorMessage = string.Format(TranslationSource.Instance["ConnectionFlow.Error.UnexpectedNetworkError"], ex.WebSocketErrorCode);
                else if (ex.ErrorCode != 0)
                    errorMessage = string.Format(TranslationSource.Instance["ConnectionFlow.Error.UnexpectedNetworkError"], ex.ErrorCode);
                else if (ex.InnerException is Win32Exception)
                    errorMessage = string.Format(TranslationSource.Instance["ConnectionFlow.Error.UnexpectedNetworkError"], "native " + (ex.InnerException as Win32Exception).NativeErrorCode);
            }
            catch (Exception ex)
            {
                errorMessage = HideezExceptionLocalization.GetErrorAsString(ex);
            }
            finally
            {
                if (device != null)
                {
                    device.Disconnected -= OnVaultDisconnectedDuringFlow;
                    device.OperationCancelled -= OnCancelledByVaultButton;
                }
                _screenActivator?.StopPeriodicScreenActivation();
            }

            await WorkflowCleanup(errorMessage, connectionId, device, workflowFinishedSuccessfully, deleteVaultBondOnError, cleanupNotifications);

            IsRunning = false;
            Finished?.Invoke(this, flowId);

            WriteLine($"Main workflow end ({connectionId.Id}, {(DefaultConnectionIdProvider)connectionId.IdProvider})");
        }

        async Task WorkflowCleanup(string errorMessage, ConnectionId connectionId, IDevice device, bool workflowFinishedSuccessfully, bool deleteVaultBond, bool cleanupNotifications)
        {
            // Cleanup
            try
            {
                await _ui.HidePinUi();

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    if (device != null && !string.IsNullOrWhiteSpace(device.SerialNo))
                    {
                        var sb = new StringBuilder();
                        sb.Append(errorMessage);
                        sb.Append(Environment.NewLine);
                        sb.Append(Environment.NewLine);
                        sb.Append(string.Format(TranslationSource.Instance["ConnectionFlow.VaultSerialNo"], device.SerialNo));

                        errorMessage = sb.ToString();
                    }

                    WriteLine(errorMessage);
                    if (device != null)
                        await _ui.SendError(errorMessage, device.Id);
                    else
                        await _ui.SendError(errorMessage, connectionId.Id);
                    await _ui.SendNotification(string.Empty, string.Empty);
                }

                if (device != null)
                {
                    if (workflowFinishedSuccessfully)
                    {
                        WriteLine($"Successfully finished the main workflow: ({device.Id})");
                        await _ui.SendError(string.Empty, device.Id);
                        await _ui.SendNotification(string.Empty, device.Id);
                        DeviceFinishedMainFlow?.Invoke(this, device);
                    }
                    else if (deleteVaultBond)
                    {
                        WriteLine($"Mainworkflow critical error, Removing ({device.Id})");
                        await _deviceManager.DeleteBond(device.DeviceConnection);
                    }
                    else
                    {
                        WriteLine($"Main workflow failed, Disconnecting ({device.Id})");
                        await _deviceManager.Disconnect(device.DeviceConnection);
                    }
                }

                if (cleanupNotifications)
                {
                    await _ui.SendError(string.Empty, device.Id);
                    await _ui.SendNotification(string.Empty, device.Id);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex, LogErrorSeverity.Error);
            }
        }
    }
}
