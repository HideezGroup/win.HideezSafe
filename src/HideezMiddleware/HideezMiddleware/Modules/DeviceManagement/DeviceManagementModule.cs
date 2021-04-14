using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.IPC.DTO;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.Modules.DeviceManagement.Messages;
using HideezMiddleware.Modules.Hes.Messages;
using HideezMiddleware.Modules.ServiceEvents.Messages;
using HideezMiddleware.Threading;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.DeviceManagement
{
    public sealed class DeviceManagementModule : ModuleBase
    {
        readonly DeviceManager _deviceManager;

        public DeviceManagementModule(DeviceManager deviceManager, IMetaPubSub messenger, ILog log)
            : base(messenger, nameof(DeviceManagementModule), log)
        {
            _deviceManager = deviceManager;

            _deviceManager.DeviceAdded += DevicesManager_DeviceAdded;
            _deviceManager.DeviceRemoved += DevicesManager_DeviceRemoved;

            _messenger.Subscribe(GetSafeHandler<HesAccessManager_AccessRetractedMessage>(HesAccessManager_AccessRetracted));

            _messenger.Subscribe(GetSafeHandler<GetAvailableChannelsMessage>(GetAvailableChannels));
            _messenger.Subscribe(GetSafeHandler<DisconnectDeviceMessage>(DisconnectDevice));
            _messenger.Subscribe(GetSafeHandler<RemoveDeviceMessage>(RemoveDeviceAsync));
            _messenger.Subscribe(GetSafeHandler<HesAppConnection_AlarmMessage>(OnAlarm));
            _messenger.Subscribe(GetSafeHandler<SessionSwitchMonitor_SessionSwitchMessage>(OnSessionSwitch));
            _messenger.Subscribe(GetSafeHandler<PowerEventMonitor_SystemSuspendingMessage>(OnSuspending));
        }

        private readonly SemaphoreQueue _devicesSemaphore = new SemaphoreQueue(1, 1);
        private async void DevicesManager_DeviceAdded(object sender, DeviceAddedEventArgs e)
        {
            await _devicesSemaphore.WaitAsync();
            try
            {
                if (e.Device != null)
                {
                    // TODO: check, who is responsible for handling these events
                    e.Device.ConnectionStateChanged += Device_ConnectionStateChanged;
                    e.Device.Initialized += Device_Initialized;
                    e.Device.Disconnected += Device_Disconnected;
                    e.Device.OperationCancelled += Device_OperationCancelled;
                    e.Device.ProximityChanged += Device_ProximityChanged;
                    e.Device.BatteryChanged += Device_BatteryChanged;
                    e.Device.WipeFinished += Device_WipeFinished;
                    e.Device.AccessLevelChanged += Device_AccessLevelChanged;
                }

                await SafePublish(new DeviceManager_DeviceAddedMessage(_deviceManager, e.Device));
            }
            finally
            {
                _devicesSemaphore.Release();
            }
        }

        private async void DevicesManager_DeviceRemoved(object sender, DeviceRemovedEventArgs e)
        {
            await _devicesSemaphore.WaitAsync();
            try
            {
                if (e.Device != null)
                {
                    // TODO: check, who is responsible for handling these events
                    e.Device.ConnectionStateChanged -= Device_ConnectionStateChanged;
                    e.Device.Initialized -= Device_Initialized;
                    e.Device.Disconnected -= Device_Disconnected;
                    e.Device.OperationCancelled -= Device_OperationCancelled;
                    e.Device.ProximityChanged -= Device_ProximityChanged;
                    e.Device.BatteryChanged -= Device_BatteryChanged;
                    e.Device.WipeFinished -= Device_WipeFinished;
                    e.Device.AccessLevelChanged -= Device_AccessLevelChanged;
                }

                await SafePublish(new DeviceManager_DeviceRemovedMessage(_deviceManager, e.Device));
            }
            finally
            {
                _devicesSemaphore.Release();
            }
        }

        private async void Device_ConnectionStateChanged(object sender, EventArgs e)
        {
            try
            {
                if (sender is IDevice device)
                {
                    if (!device.IsConnected)
                        device.SetUserProperty(CustomProperties.HW_CONNECTION_STATE_PROP, HwVaultConnectionState.Offline);

                    await SafePublish(new DeviceConnectionStateChangedMessage(new DeviceDTO(device)));
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        private async void Device_Initialized(object sender, EventArgs e)
        {
            try
            {
                if (sender is IDevice device)
                {
                    // Separate error handling block for each callback ensures we try to notify 
                    // every session, even if an error occurs
                    try
                    {
                        await SafePublish(new DeviceInitializedMessage(new DeviceDTO(device)));
                    }
                    catch (Exception ex)
                    {
                        WriteLine(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        private async void Device_Disconnected(object sender, EventArgs e)
        {
            try
            {
                if (sender is IDevice device)
                {
                    try
                    {
                        if (device.ChannelNo == (byte)DefaultDeviceChannel.Main && device.GetUserProperty<bool>(CustomProperties.HW_WIPE_STATE_PROP))
                        {
                            try
                            {
                                // Wiped device is cleared of all bond information, and therefore must be paired again
                                await _deviceManager.DeleteBond(device.DeviceConnection);
                            }
                            catch (Exception ex)
                            {
                                WriteLine(ex);
                            }
                        }
                        await SafePublish(new DeviceDisconnectedMessage(new DeviceDTO(device)));
                    }
                    catch (Exception ex)
                    {
                        WriteLine(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        private async void Device_OperationCancelled(object sender, EventArgs e)
        {
            if (sender is IDevice device)
                await SafePublish(new DeviceOperationCancelledMessage(new DeviceDTO(device)));
        }

        private async void Device_ProximityChanged(object sender, double e)
        {
            if (sender is IDevice device)
                await SafePublish(new DeviceProximityChangedMessage(device.Id, e));
        }

        private async void Device_BatteryChanged(object sender, sbyte e)
        {
            if (sender is IDevice device)
                await SafePublish(new DeviceBatteryChangedMessage(device.Id, device.Mac, e));
        }

        private async void Device_WipeFinished(object sender, WipeFinishedEventtArgs e)
        {
            if (e.Status == FwWipeStatus.WIPE_OK)
            {
                var device = (IDevice)sender;
                if (device.ChannelNo == (byte)DefaultDeviceChannel.Main)
                {
                    device.SetUserProperty(CustomProperties.HW_WIPE_STATE_PROP, true);
                    WriteLine($"({device.SerialNo}) Wipe finished. Disabling automatic reconnect");
                    await _messenger.Publish(new DeviceManager_ExpectedDeviceRemovalMessage(device));
                }
            }
        }

        private async void Device_AccessLevelChanged(object sender, AccessLevel e)
        {
            var device = (IDevice)sender;
            if (device.ChannelNo == (int)DefaultDeviceChannel.Main)
            {
                try
                {
                    await device.RefreshDeviceInfo();
                }
                catch { }
            }
        }


        // Disconnect all devices when access is retracted
        private async Task HesAccessManager_AccessRetracted(HesAccessManager_AccessRetractedMessage msg)
        {
            try
            {
                foreach (var device in _deviceManager.Devices)
                {
                    await _messenger.Publish(new DeviceManager_ExpectedDeviceRemovalMessage(device));
                    await _deviceManager.Disconnect(device.DeviceConnection).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        async Task GetAvailableChannels(GetAvailableChannelsMessage args)
        {
            // Channels range from 1 to 6 
            List<byte> freeChannels = new List<byte>() { 1, 2, 3, 4, 5, 6 };

            try
            {
                var devices = _deviceManager.Devices.Where(d => d.SerialNo == args.SerialNo).ToList();
                if (devices.Count == 0)
                    throw new HideezException(HideezErrorCode.DeviceNotFound, args.SerialNo);

                // These channels are reserved by system, the rest is available to clients
                freeChannels.Remove((byte)DefaultDeviceChannel.Main);
                freeChannels.Remove((byte)DefaultDeviceChannel.HES);

                // Filter out taken channels
                var channelsInUse = devices.Select(d => d.ChannelNo).ToList();
                freeChannels.RemoveAll(c => channelsInUse.Contains(c));
            }
            catch (Exception ex)
            {
                WriteLine(ex);
                throw;
            }

            await SafePublish(new GetAvailableChannelsMessageReply(freeChannels.ToArray()));
        }

        async Task DisconnectDevice(DisconnectDeviceMessage args)
        {
            try
            {
                var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == args.DeviceId);
                if (device != null)
                {
                    await _messenger.Publish(new DeviceManager_ExpectedDeviceRemovalMessage(device));
                    await _deviceManager.Disconnect(device.DeviceConnection);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
                throw;
            }
        }

        async Task RemoveDeviceAsync(RemoveDeviceMessage args)
        {
            try
            {
                var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == args.DeviceId);
                if (device != null)
                {
                    await _messenger.Publish(new DeviceManager_ExpectedDeviceRemovalMessage(device));
                    await _deviceManager.DeleteBond(device.DeviceConnection);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
                throw;
            }
        }

        async Task OnAlarm(HesAppConnection_AlarmMessage msg)
        {
            if (msg.IsEnabled)
            {
                foreach (var device in _deviceManager.Devices)
                {
                    await _messenger.Publish(new DeviceManager_ExpectedDeviceRemovalMessage(device));
                    await _deviceManager.DeleteBond(device.DeviceConnection);
                }
            }
        }

        // Disconnect all devices when user is logged out or session is locked
        private async Task OnSessionSwitch(SessionSwitchMonitor_SessionSwitchMessage msg)
        {
            if (msg.Reason == SessionSwitchReason.SessionLogoff || msg.Reason == SessionSwitchReason.SessionLock)
            {
                // Disconnect all connected devices
                foreach (var device in _deviceManager.Devices)
                    await _deviceManager.Disconnect(device.DeviceConnection);
            }
        }

        // Disconnect all devices when system is suspending
        private async Task OnSuspending(PowerEventMonitor_SystemSuspendingMessage msg)
        {
            foreach (var device in _deviceManager.Devices)
                await _deviceManager.Disconnect(device.DeviceConnection);
        }
    }
}
