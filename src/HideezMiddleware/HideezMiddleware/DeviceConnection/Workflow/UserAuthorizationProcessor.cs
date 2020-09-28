﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.Localize;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.Workflow
{
    public class UserAuthorizationProcessor : Logger
    {
        readonly UiProxyManager _ui;

        public UserAuthorizationProcessor(UiProxyManager ui, ILog log)
            : base(nameof(UserAuthorizationProcessor), log)
        {
            _ui = ui;
        }

        public async Task AuthorizeUser(IDevice device, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            int timeout = SdkConfig.MainWorkflowTimeout;
            
            await ButtonWorkflow(device, timeout, ct);
            await PinWorkflow(device, timeout, ct);
            await ButtonWorkflow(device, timeout, ct);

            // A precaution, but we shouldn't reach this line
            if (device.AccessLevel.IsLocked || !device.AccessLevel.IsButtonRequired || device.AccessLevel.IsPinRequired)
                throw new VaultFailedToAuthorizeException(TranslationSource.Instance["ConnectionFlow.UserAuthorization.Error.AuthFailed"]);
        }

        async Task<bool> ButtonWorkflow(IDevice device, int timeout, CancellationToken ct)
        {
            if (!device.AccessLevel.IsButtonRequired)
                return true;

            await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.Button.PressButtonMessage"], device.Mac);
            await _ui.ShowButtonConfirmUi(device.Id);
            var res = await device.WaitButtonConfirmation(timeout, ct);

            ct.ThrowIfCancellationRequested();

            return res;
        }

        async Task PinWorkflow(IDevice device, int timeout, CancellationToken ct)
        {
            if (device.AccessLevel.IsPinRequired && device.AccessLevel.IsNewPinRequired)
            {
                await SetPinWorkflow(device, timeout, ct);
            }
            else if (device.AccessLevel.IsPinRequired)
            {
                await EnterPinWorkflow(device, timeout, ct);
            }
        }

        async Task SetPinWorkflow(IDevice device, int timeout, CancellationToken ct)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>> SetPinWorkflow +++++++++++++++++++++++++++++++++++++++");

            while (device.AccessLevel.IsNewPinRequired)
            {
                await _ui.SendNotification(TranslationSource.Instance.Format("ConnectionFlow.Pin.NewPinMessage", device.MinPinLength), device.Mac);
                string pin = await _ui.GetPin(device.Id, timeout, ct, withConfirm: true);

                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(pin))
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    WriteLine("Received empty PIN");
                    continue;
                }

                var pinResult = await device.SetPin(pin); //this using default timeout for BLE commands

                ct.ThrowIfCancellationRequested();

                if (pinResult == HideezErrorCode.Ok)
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> PIN OK");
                    break;
                }
                else if (pinResult == HideezErrorCode.ERR_PIN_TOO_SHORT)
                {
                    await _ui.SendError(TranslationSource.Instance["ConnectionFlow.Pin.Error.PinToShort"], device.Mac);
                }
                else if (pinResult == HideezErrorCode.ERR_PIN_WRONG)
                {
                    await _ui.SendError(TranslationSource.Instance["ConnectionFlow.Pin.Error.WrongPin"], device.Mac);
                }
            }
            Debug.WriteLine(">>>>>>>>>>>>>>> SetPinWorkflow ---------------------------------------");
        }

        async Task EnterPinWorkflow(IDevice device, int timeout, CancellationToken ct)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>> EnterPinWorkflow +++++++++++++++++++++++++++++++++++++++");

            while (!device.AccessLevel.IsLocked)
            {
                await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.Pin.EnterPinMessage"], device.Mac);
                string pin = await _ui.GetPin(device.Id, timeout, ct);

                ct.ThrowIfCancellationRequested();

                Debug.WriteLine($">>>>>>>>>>>>>>> PIN: {pin}");
                if (string.IsNullOrWhiteSpace(pin))
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    Debug.WriteLine($">>>>>>>>>>>>>>> EMPTY PIN");
                    WriteLine("Received empty PIN");
                    continue;
                }

                var attemptsLeft = device.PinAttemptsRemain - 1;
                var pinResult = await device.EnterPin(pin); //this using default timeout for BLE commands

                ct.ThrowIfCancellationRequested();

                if (pinResult == HideezErrorCode.Ok)
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> PIN OK");
                    break;
                }
                else if (pinResult == HideezErrorCode.ERR_DEVICE_LOCKED_BY_PIN)
                {
                    throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.Pin.Error.LockedByInvalidAttempts"]);
                }
                else // ERR_PIN_WRONG and ERR_PIN_TOO_SHORT should just be displayed as wrong pin for security reasons
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> Wrong PIN ({attemptsLeft} attempts left)");
                    if (device.AccessLevel.IsLocked)
                    {
                        throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.Pin.Error.LockedByInvalidAttempts"]);
                    }
                    else
                    {
                        if (attemptsLeft > 1)
                            await _ui.SendError(TranslationSource.Instance.Format("ConnectionFlow.Pin.Error.InvalidPin.ManyAttemptsLeft", attemptsLeft), device.Mac);
                        else
                            await _ui.SendError(TranslationSource.Instance["ConnectionFlow.Pin.Error.InvalidPin.OneAttemptLeft"], device.Mac);
                        await device.RefreshDeviceInfo(); // Remaining pin attempts update is not quick enough 
                    }
                }
            }
            Debug.WriteLine(">>>>>>>>>>>>>>> PinWorkflow ------------------------------");
        }
    }
}
