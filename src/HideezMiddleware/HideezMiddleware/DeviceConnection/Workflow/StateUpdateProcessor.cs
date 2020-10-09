﻿using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using HideezMiddleware.Localize;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.Workflow
{
    public class StateUpdateProcessor : Logger, IStateUpdateProcessor
    {
        readonly IHesAppConnection _hesConnection;

        public StateUpdateProcessor(IHesAppConnection hesConnection, ILog log)
            : base(nameof(StateUpdateProcessor), log)
        {
            _hesConnection = hesConnection;
        }

        public async Task<HwVaultInfoFromHesDto> UpdateVaultStatus(IDevice device, HwVaultInfoFromHesDto vaultInfo, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            HwVaultInfoFromHesDto newVaultInfo = null;

            if (vaultInfo.NeedStateUpdate && _hesConnection.State == HesConnectionState.Connected)
            {
                newVaultInfo = await _hesConnection.UpdateHwVaultStatus(new HwVaultInfoFromClientDto(device), ct);
                await device.RefreshDeviceInfo();
            }

            if (device.AccessLevel.IsLinkRequired)
            {
                if (_hesConnection.State == HesConnectionState.Connected)
                    throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.StateUpdate.Error.NotAssignedToUser"]);
                else
                    throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.StateUpdate.Error.CannotLinkToUser"]);
            }

            return newVaultInfo ?? vaultInfo;
        }
    }
}
