using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using HideezMiddleware.Localize;
using Meta.Lib.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Connection;

namespace HideezMiddleware.DeviceConnection.Workflow
{
    public class VaultConnectionProcessor : Logger, IVaultConnectionProcessor
    {
        readonly IClientUiManager _ui;
        readonly BondManager _bondManager;
        readonly DeviceManager _deviceManager;

        public VaultConnectionProcessor(IClientUiManager ui, BondManager bondManager, DeviceManager deviceManager, ILog log)
            : base(nameof(VaultConnectionProcessor), log)
        {
            _ui = ui;
            _bondManager = bondManager;
            _deviceManager = deviceManager;
        }

        public async Task<IDevice> ConnectVault(ConnectionId connectionId, bool rebondOnFail, CancellationToken ct)
        {
            if (connectionId.IdProvider == (byte)DefaultConnectionIdProvider.Csr)
                return await ConnectVaultByCsr(connectionId, rebondOnFail, ct);
            else
                return await ConnectVaultByWinBle(connectionId, ct);
        }

        async Task<IDevice> ConnectVaultByCsr(ConnectionId connectionId, bool rebondOnFail, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (!_bondManager.Exists(connectionId))
                await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.Connection.Stage1.PressButton"], connectionId.Id);
            else
                await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.Connection.Stage1"], connectionId.Id);

            bool ltkErrorOccured = false;
            IDevice device = null;

            var connectionTimeout = SdkConfig.ConnectDeviceTimeout;

            try
            {
                device = await _deviceManager.Connect(connectionId).TimeoutAfter(connectionTimeout);
            }
            catch (TimeoutException)
            {
                WriteLine($"Connection attempt timed out");
            }
            catch (Exception ex) // Thrown when LTK error occurs in csr
            {
                WriteLine(ex);
                ltkErrorOccured = true;
            }

            if (device == null)
            {
                ct.ThrowIfCancellationRequested();

                string ltk = "";
                if (ltkErrorOccured)
                {
                    ltk = "LTK error.";
                    ltkErrorOccured = false;
                }
                if (!_bondManager.Exists(connectionId))
                    await _ui.SendNotification(ltk + TranslationSource.Instance["ConnectionFlow.Connection.Stage2.PressButton"], connectionId.Id);
                else
                    await _ui.SendNotification(ltk + TranslationSource.Instance["ConnectionFlow.Connection.Stage2"], connectionId.Id);

                try
                {
                    device = await _deviceManager.Connect(connectionId).TimeoutAfter(connectionTimeout / 2);
                }
                catch (TimeoutException)
                {
                    WriteLine("Connection attempt timed out");
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                    ltkErrorOccured = true;
                }

                // Only for csr
                // After second failed connect with csr we delete bond and try to create new pair
                if (device == null && rebondOnFail)
                {
                    ct.ThrowIfCancellationRequested();

                    // remove the bond and try one more time
                    await _deviceManager.DeleteBond(connectionId);

                    if (ltkErrorOccured)
                        await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.Connection.Stage3.LtkError.PressButton"], connectionId.Id); // TODO: Fix LTK error in CSR
                    else
                        await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.Connection.Stage3.PressButton"], connectionId.Id);

                    device = await _deviceManager.Connect(connectionId).TimeoutAfter(connectionTimeout);
                }
            }

            if (device == null)
                throw new WorkflowException(TranslationSource.Instance.Format("ConnectionFlow.Connection.ConnectionFailed.Csr", connectionId.Id));

            await _ui.SendNotification(string.Empty, connectionId.Id);

            return device;
        }

        async Task<IDevice> ConnectVaultByWinBle(ConnectionId connectionId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.Connection.Stage1"], connectionId.Id);

            IDevice device = null;

            var connectionTimeout = SdkConfig.ConnectDeviceTimeout * 2;

            try
            {
                device = await _deviceManager.Connect(connectionId).TimeoutAfter(connectionTimeout);
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }

            if (device == null)
            {
                ct.ThrowIfCancellationRequested();

                await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.Connection.Stage2"], connectionId.Id);

                try
                {
                    device = await _deviceManager.Connect(connectionId).TimeoutAfter(connectionTimeout / 2);
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                }
            }

            if (device == null)
                throw new WorkflowException(TranslationSource.Instance.Format("ConnectionFlow.Connection.ConnectionFailed", connectionId.Id));

            await _ui.SendNotification(string.Empty, connectionId.Id);

            return device;
        }

        public async Task WaitVaultInitialization(IDevice device, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.Initialization.WaitingInitializationMessage"], device.Id);

            if (!await device.WaitInitialization(SdkConfig.DeviceInitializationTimeout, ct))
                throw new WorkflowException(TranslationSource.Instance.Format("ConnectionFlow.Initialization.InitializationFailed", device.Id));

            if (device.IsErrorState)
            {
                await _deviceManager.RemoveConnection(device.DeviceConnection);
                throw new WorkflowException(TranslationSource.Instance.Format("ConnectionFlow.Initialization.DeviceInitializationError", device.Id, device.ErrorMessage));
            }
        }
    }
}
