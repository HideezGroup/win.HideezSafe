﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.PasswordManager;
using Hideez.SDK.Communication.Utils;
using Microsoft.Win32;

namespace HideezMiddleware
{
    public class ConnectionFlowProcessor : Logger
    {
        public const string FLOW_FINISHED_PROP = "MainFlowFinished"; 

        readonly BleDeviceManager _deviceManager;
        readonly IWorkstationUnlocker _workstationUnlocker;
        readonly IScreenActivator _screenActivator;
        readonly UiProxyManager _ui;
        readonly HesAppConnection _hesConnection;

        int _isConnecting = 0;
        string _infNid = string.Empty; // Notification Id, which must be the same for the entire duration of MainWorkflow
        string _errNid = string.Empty; // Error Notification Id
        CancellationTokenSource _cts;

        public event EventHandler<IDevice> DeviceFinishedMainFlow;

        public ConnectionFlowProcessor(BleDeviceManager deviceManager,
            HesAppConnection hesConnection,
            IWorkstationUnlocker workstationUnlocker,
            IScreenActivator screenActivator,
            UiProxyManager ui,
            ILog log)
            : base(nameof(ConnectionFlowProcessor), log)
        {
            _deviceManager = deviceManager;
            _workstationUnlocker = workstationUnlocker;
            _screenActivator = screenActivator;
            _ui = ui;
            _hesConnection = hesConnection;

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            Cancel();
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }

        public async Task<ConnectionFlowResult> ConnectAndUnlock(string mac)
        {
            // ignore, if workflow for any device already initialized
            if (Interlocked.CompareExchange(ref _isConnecting, 1, 0) == 0)
            {
                try
                {
                    _cts = new CancellationTokenSource();
                    return await MainWorkflow(mac, _cts.Token);
                }
                finally
                {
                    // this delay allows a user to move away the device from the dongle or RFID
                    // and prevents the repeated call of this method
                    await Task.Delay(SdkConfig.DelayAfterMainWorkflow);

                    _cts.Dispose();
                    _cts = null;

                    Interlocked.Exchange(ref _isConnecting, 0);
                }
            }

            return new ConnectionFlowResult();
        }

        async Task<ConnectionFlowResult> MainWorkflow(string mac, CancellationToken ct)
        {
            // Ignore MainFlow requests for devices that are already connected
            // IsConnected-true indicates that device already finished main flow or is in progress
            var existingDevice = _deviceManager.Find(mac, (int)DefaultDeviceChannel.Main); 
            if (existingDevice != null && existingDevice.IsConnected)
                return new ConnectionFlowResult();

            Debug.WriteLine(">>>>>>>>>>>>>>> MainWorkflow +++++++++++++++++++++++++");

            bool success = false;
            var flowResult = new ConnectionFlowResult();
            bool fatalError = false;
            string errorMessage = null;
            IDevice device = null;
            _infNid = Guid.NewGuid().ToString();
            _errNid = Guid.NewGuid().ToString();

            try
            {
                await _ui.SendNotification("", _infNid);
                await _ui.SendError("", _errNid);

                _screenActivator?.ActivateScreen();
                device = await ConnectDevice(mac, ct);

                device.Disconnected += (object sender, EventArgs e) =>
                {
                    // cancel the workflow if the device disconnects
                    Cancel();
                };

                await WaitDeviceInitialization(mac, device);

                if (device.AccessLevel.IsLocked || device.AccessLevel.IsLinkRequired)
                {
                    // request hes to update this device
                    await _hesConnection.FixDevice(device);
                    await device.RefreshDeviceInfo();
                }

                if (device.AccessLevel.IsLocked)
                    throw new HideezException(HideezErrorCode.DeviceIsLocked);

                if (device.AccessLevel.IsLinkRequired)
                    throw new HideezException(HideezErrorCode.DeviceNotAssignedToUser);


                if (await IsNeedUpdateDevice(device))
                {
                    // request HES to update this device
                    await _ui.SendNotification("Uploading new credentials to the device...", _infNid);
                    await _hesConnection.FixDevice(device);
                }

                int timeout = SdkConfig.MainWorkflowTimeout;

                await MasterKeyWorkflow(device, ct);

                if (_workstationUnlocker.IsConnected)
                {
                    if (await ButtonWorkflow(device, timeout, ct) && await PinWorkflow(device, timeout, ct))
                    {
                        // check the button again as it may be outdated while PIN workflow was running
                        await ButtonWorkflow(device, timeout, ct);//todo - fix FW

                        if (!device.AccessLevel.IsLocked &&
                            !device.AccessLevel.IsButtonRequired &&
                            !device.AccessLevel.IsPinRequired &&
                            !device.AccessLevel.IsNewPinRequired)
                        {
                            success = await TryUnlockWorkstation(device);
                            flowResult.UnlockSuccessful = success;
                        }
                    }
                }
                else
                {
                    success = true;
                }
            }
            catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.DeviceNotAssignedToUser)
            {
                fatalError = true;
                errorMessage = HideezExceptionLocalization.GetErrorAsString(ex);
            }
            catch (HideezException ex)
            {
                errorMessage = HideezExceptionLocalization.GetErrorAsString(ex);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }


            try
            {
                await _ui.HidePinUi();
                await _ui.SendNotification("", _infNid);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    WriteLine(errorMessage);
                    await _ui.SendError(errorMessage, _errNid);
                }

                if (device != null)
                {
                    if (fatalError)
                    {
                        await _deviceManager.Remove(device);
                        flowResult.ConnectSuccessful = false;
                    }
                    else if (!success)
                    {
                        await device.Disconnect();
                        flowResult.ConnectSuccessful = false;
                    }
                    else
                    {
                        device.SetUserProperty(FLOW_FINISHED_PROP, true);
                        DeviceFinishedMainFlow?.Invoke(this, device);
                        flowResult.ConnectSuccessful = true;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex, LogErrorSeverity.Fatal);
            }
            finally
            {
                _infNid = string.Empty;
                _errNid = string.Empty;
            }

            Debug.WriteLine(">>>>>>>>>>>>>>> MainWorkflow ------------------------------");

            return flowResult;
        }

        async Task<bool> TryUnlockWorkstation(IDevice device)
        {
            await _ui.SendNotification("Reading credentials from the device...", _infNid);
            var credentials = await GetCredentials(device);

            // send credentials to the Credential Provider to unlock the PC
            await _ui.SendNotification("Unlocking the PC...", _infNid);
            var success = await _workstationUnlocker
                .SendLogonRequest(credentials.Login, credentials.Password, credentials.PreviousPassword);

            return success;
        }

        //async Task<bool> TryUnlockWorkstation(IDevice device)
        //{
        //    await _ui.SendNotification("Reading credentials from the device...", _infNid);

        //    // read in parallel info from the HES and credentials from the device
        //    var infoTask = IsNeedUpdateDevice(device);
        //    var credentialsTask = GetCredentials(device);

        //    await Task.WhenAll(infoTask, credentialsTask);

        //    var credentials = credentialsTask.Result;

        //    // if the device needs to be updated, update and read credentials again
        //    if (infoTask.Result)
        //    {
        //        // request hes to update this device
        //        await _hesConnection.FixDevice(device);

        //        await WaitForRemoteDeviceUpdate(device.SerialNo);

        //        credentials = await GetCredentials(device);
        //    }

        //    // send credentials to the Credential Provider to unlock the PC
        //    await _ui.SendNotification("Unlocking the PC...", _infNid);
        //    var success = await _workstationUnlocker
        //        .SendLogonRequest(credentials.Login, credentials.Password, credentials.PreviousPassword);

        //    return success;
        //}

        async Task<bool> IsNeedUpdateDevice(IDevice device)
        {
            try
            {
                if (_hesConnection.State == HesConnectionState.Connected)
                {
                    var info = await _hesConnection.GetInfoBySerialNo(device.SerialNo);
                    return info.NeedUpdate;
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }

            return false;
        }

        async Task MasterKeyWorkflow(IDevice device, CancellationToken ct)
        {
            if (!device.AccessLevel.IsMasterKeyRequired)
                return;

            ct.ThrowIfCancellationRequested();

            await _ui.SendNotification("Waiting for HES authorization...", _infNid);
            await _hesConnection.FixDevice(device);
            await _ui.SendNotification("", _infNid);
        }

        async Task<bool> ButtonWorkflow(IDevice device, int timeout, CancellationToken ct)
        {
            if (!device.AccessLevel.IsButtonRequired)
                return true;

            await _ui.SendNotification("Please press the Button on your Hideez Key", _infNid);
            await _ui.ShowButtonConfirmUi(device.Id);
            var res = await device.WaitButtonConfirmation(timeout, ct);
            return res;
        }

        Task<bool> PinWorkflow(IDevice device, int timeout, CancellationToken ct)
        {
            if (device.AccessLevel.IsNewPinRequired)
            {
                return SetPinWorkflow(device, timeout, ct);
            }
            else if (device.AccessLevel.IsPinRequired)
            {
                return EnterPinWorkflow(device, timeout, ct);
            }

            return Task.FromResult(true);
        }

        async Task<bool> SetPinWorkflow(IDevice device, int timeout, CancellationToken ct)
        {
            bool res = false;
            while (device.AccessLevel.IsNewPinRequired)
            {
                await _ui.SendNotification("Please create new PIN code for your Hideez Key", _infNid);
                string pin = await _ui.GetPin(device.Id, timeout, ct, withConfirm: true);

                if (string.IsNullOrWhiteSpace(pin))
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    Debug.WriteLine($">>>>>>>>>>>>>>> EMPTY PIN");
                    WriteLine("Received empty PIN");
                    continue;
                }

                res = await device.SetPin(pin); //this using default timeout for BLE commands
            }
            return res;
        }

        async Task<bool> EnterPinWorkflow(IDevice device, int timeout, CancellationToken ct)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>> EnterPinWorkflow +++++++++++++++++++++++++++++++++++++++");

            bool res = false;
            while (!device.AccessLevel.IsLocked)
            {
                await _ui.SendNotification("Please enter the PIN code for your Hideez Key", _infNid);
                string pin = await _ui.GetPin(device.Id, timeout, ct);

                Debug.WriteLine($">>>>>>>>>>>>>>> PIN: {pin}");
                if (string.IsNullOrWhiteSpace(pin))
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    Debug.WriteLine($">>>>>>>>>>>>>>> EMPTY PIN");
                    WriteLine("Received empty PIN");

                    continue;
                }

                res = await device.EnterPin(pin); //this using default timeout for BLE commands

                if (res)
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> PIN OK");
                    break;
                }
                else
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> Wrong PIN ({device.PinAttemptsRemain} attempts left)");
                    if (device.AccessLevel.IsLocked)
                        await _ui.SendError($"Device is locked", _errNid);
                    else
                        await _ui.SendError($"Wrong PIN ({device.PinAttemptsRemain} attempts left)", _errNid);
                }
            }
            Debug.WriteLine(">>>>>>>>>>>>>>> PinWorkflow ------------------------------");
            return res;
        }

        async Task<IDevice> ConnectDevice(string mac, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _ui.SendNotification("Connecting to the device...", _infNid);

            var device = await _deviceManager.ConnectByMac(mac, BleDefines.ConnectDeviceTimeout);

            if (device == null)
            {
                ct.ThrowIfCancellationRequested();
                await _ui.SendNotification("Connection failed. Retrying...", _infNid);
                device = await _deviceManager.ConnectByMac(mac, BleDefines.ConnectDeviceTimeout / 2);
            }

            if (device == null)
            {
                ct.ThrowIfCancellationRequested();
                // remove the bond and try one more time
                await _deviceManager.RemoveByMac(mac);
                await _ui.SendNotification("Connection failed. Trying re-bond the device...", _infNid);
                device = await _deviceManager.ConnectByMac(mac, BleDefines.ConnectDeviceTimeout);
            }

            if (device == null)
                throw new Exception($"Failed to connect device '{mac}'.");

            return device;
        }

        async Task WaitDeviceInitialization(string mac, IDevice device)
        {
            await _ui.SendNotification("Waiting for the device initialization...", _infNid);

            if (!await device.WaitInitialization(BleDefines.DeviceInitializationTimeout))
                throw new Exception($"Failed to initialize device connection '{mac}'. Please try again.");

            if (device.IsErrorState)
            {
                await _deviceManager.Remove(device);
                throw new Exception($"Failed to initialize device connection '{mac}' ({device.ErrorMessage}). Please try again.");
            }
        }

        async Task<Credentials> GetCredentials(IDevice device)
        {
            ushort primaryAccountKey = await DevicePasswordManager.GetPrimaryAccountKey(device);
            var credentials = await GetCredentials(device, primaryAccountKey);
            return credentials;
        }

        async Task<Credentials> GetCredentials(IDevice device, ushort key)
        {
            Credentials credentials;

            if (key == 0)
            {
                var str = await device.ReadStorageAsString(
                    (byte)StorageTable.BondVirtualTable1,
                    (ushort)BondVirtualTable1Item.PcUnlockCredentials);

                if (str != null)
                {
                    var parts = str.Split('\n');
                    if (parts.Length >= 2)
                    {
                        credentials.Login = parts[0];
                        credentials.Password = parts[1];
                    }
                    if (parts.Length >= 3)
                    {
                        credentials.PreviousPassword = parts[2];
                    }
                }

                if (credentials.IsEmpty)
                    throw new Exception($"Device '{device.SerialNo}' has not a primary account stored");
            }
            else
            {
                // get the login and password from the Hideez Key
                credentials.Login = await device.ReadStorageAsString((byte)StorageTable.Logins, key);
                credentials.Password = await device.ReadStorageAsString((byte)StorageTable.Passwords, key);
                credentials.PreviousPassword = ""; //todo

                if (credentials.IsEmpty)
                    throw new Exception($"Cannot read login or password from the device '{device.SerialNo}'");
            }

            return credentials;
        }

    }
}
