﻿using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.Workflow.Interfaces
{
    public interface IVaultConnectionProcessor
    {
        /// <summary>
        /// Connect target vault to this computer
        /// </summary>
        /// <param name="connectionId">Connection id of device that will be connected</param>
        /// <param name="rebondOnFail">If true, after second error vault bond will be removed before another reconnect attempt</param>
        /// <param name="ct">CancellationToken</param>
        /// <exception cref="WorkflowException">Thrown if vault connection failed after multiple attemts</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation token is cancelled.</exception>
        Task<IDevice> ConnectVault(ConnectionId connectionId, bool rebondOnFail, CancellationToken ct);

        /// <summary>
        /// Wait until vault finishes initialization and all metadata is loaded into <see cref="IDevice"/> properties
        /// </summary>
        /// <param name="device">Vault object that awaits initialization</param>
        /// <param name="ct">CancellationToken</param>
        /// <exception cref="WorkflowException">Thrown if vault initialization failed</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation token is cancelled.</exception>
        Task WaitVaultInitialization(IDevice device, CancellationToken ct);
    }
}
