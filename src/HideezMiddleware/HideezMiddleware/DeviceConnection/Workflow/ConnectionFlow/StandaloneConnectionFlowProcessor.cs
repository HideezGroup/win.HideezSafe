using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using HideezMiddleware.DeviceLogging;
using HideezMiddleware.Localize;
using HideezMiddleware.ScreenActivation;
using HideezMiddleware.Tasks;
using HideezMiddleware.Utils.WorkstationHelper;
using Meta.Lib.Modules.PubSub;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow
{
    public sealed class StandaloneConnectionFlowProcessor : ConnectionFlowProcessorBase
    {
        public struct StandaloneConnectionFlowSubprocessorsStruct
        {
            public IVaultConnectionProcessor VaultConnectionProcessor;
            public IVaultAuthorizationProcessor MasterkeyProcessor;
            public IUserAuthorizationProcessor UserAuthorizationProcessor;
            public IUnlockProcessor UnlockProcessor;
        }

        readonly DeviceManager _deviceManager;
        readonly IWorkstationUnlocker _workstationUnlocker; // Todo: remove and replace calls with unlockProcessor
        readonly IScreenActivator _screenActivator;
        readonly IClientUiManager _ui;
        readonly IWorkstationHelper _workstationHelper;
        private readonly IDeviceProximitySettingsProvider _proximitySettingsProvider;
        readonly DeviceLogManager _deviceLogManager;

        readonly StandaloneConnectionFlowSubprocessorsStruct _subp;

        public override event EventHandler<string> Started;
        public override event EventHandler<IDevice> DeviceFinilizingMainFlow;
        public override event EventHandler<IDevice> DeviceFinishedMainFlow;
        public override event EventHandler<string> Finished;
        public override event EventHandler<string> AttemptingUnlock;
        public override event EventHandler<string> UnlockAttempted;

        public StandaloneConnectionFlowProcessor(
            DeviceManager deviceManager,
            IWorkstationUnlocker workstationUnlocker,
            IScreenActivator screenActivator,
            IClientUiManager ui,
            StandaloneConnectionFlowSubprocessorsStruct subprocs,
            IWorkstationHelper workstationHelper,
            IDeviceProximitySettingsProvider proximitySettingsProvider,
            DeviceLogManager deviceLogManager,
            IMetaPubSub messenger,
            ILog log)
            : base(messenger, nameof(StandaloneConnectionFlowProcessor), log)
        {
            _deviceManager = deviceManager;
            _workstationUnlocker = workstationUnlocker;
            _screenActivator = screenActivator;
            _ui = ui;

            _subp = subprocs;
            _workstationHelper = workstationHelper;
            _proximitySettingsProvider = proximitySettingsProvider;
            _deviceLogManager = deviceLogManager;

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

                if(device.LicenseInfo > 0)
                    throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.Error.VaultInEnterpriseMode"]);

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

                //This fix is applied to prevent spam by failed connections for WinBle.
                //deleteVaultBondOnError = IsNeedDeleteBond(device);

                device.SetUserProperty(CustomProperties.HW_CONNECTION_STATE_PROP, HwVaultConnectionState.Initializing);

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
                    await _subp.MasterkeyProcessor.AuthVault(device, ct);

                    await Task.WhenAll(_subp.UserAuthorizationProcessor.AuthorizeUser(device, ct));

                    _screenActivator?.StopPeriodicScreenActivation();
                    AttemptingUnlock?.Invoke(this, flowId);
                    await _subp.UnlockProcessor.UnlockWorkstation(device, flowId, onUnlockAttempt, ct);
                }

                device.SetUserProperty(CustomProperties.HW_CONNECTION_STATE_PROP, HwVaultConnectionState.Finalizing);
                WriteLine($"Finalizing main workflow: ({device.Id})");
                DeviceFinilizingMainFlow?.Invoke(this, device);

                device.SetUserProperty(CustomProperties.HW_CONNECTION_STATE_PROP, HwVaultConnectionState.Online);

                workflowFinishedSuccessfully = true;
            }
            catch (HideezException ex)
            {
                switch (ex.ErrorCode)
                {
                    case HideezErrorCode.DeviceIsLocked:
                    case HideezErrorCode.DeviceNotAssignedToUser:
                    case HideezErrorCode.DeviceHasBeenWiped:
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
                await _ui.HidePasswordUi();

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

        /// <summary>
        /// When PC is unlocked we need to delete the bond for the device connected via WinBle, the first connection of which 
        /// was failed and the re-fast connection will also be failed.
        /// </summary>
        /// <param name="device">The device must be initialized.</param>
        /// <param name="hesAppConnection"></param>
        /// <returns>True, if device is connected via WinBle and PC is unlocked, device has no licenses or
        /// workstation doesn't have connection to HES and device is not authorized on HES, not assigned to user or locked.
        /// False, if else.</returns>
        bool IsNeedDeleteBond(IDevice device)
        {
            if (device.DeviceConnection.Connection.ConnectionId.IdProvider == (byte)DefaultConnectionIdProvider.WinBle && !_workstationHelper.IsActiveSessionLocked())
                if (device.AccessLevel.IsMasterKeyRequired || device.IsLocked)
                    return true;

            return false;
        }
    }
}
