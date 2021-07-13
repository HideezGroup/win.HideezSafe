using Hideez.SDK.Communication;
using HideezMiddleware.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware
{
    public class SdkConfigLoader
    {
        public static async Task LoadSdkConfigFromFileAsync(ISettingsManager<SdkSettings> settingsManager)
        {
            var settings = await settingsManager.GetSettingsAsync();

            SdkConfig.MainWorkflowTimeout = settings.MainWorkflowTimeout;
            SdkConfig.CredentialProviderLogonTimeout = settings.CredentialProviderLogonTimeout;
            SdkConfig.TapProximityUnlockThreshold = settings.TapProximityUnlockThreshold;
            SdkConfig.DelayAfterMainWorkflow = settings.DelayAfterMainWorkflow;
            SdkConfig.WorkstationUnlockerConnectTimeout = settings.WorkstationUnlockerConnectTimeout;
            SdkConfig.ReconnectDelay = settings.ReconnectDelay;
            SdkConfig.HesRequestTimeout = settings.HesRequestTimeout;

            SdkConfig.DefaultCommandTimeout = settings.DefaultCommandTimeout;
            SdkConfig.DefaultRemoteCommandTimeout = settings.DefaultRemoteCommandTimeout;
            SdkConfig.VerifyCommandTimeout = settings.VerifyCommandTimeout;
            SdkConfig.GetRootKeyCommandTimeout = settings.GetRootKeyCommandTimeout;
            SdkConfig.RemoteVerifyCommandTimeout = settings.RemoteVerifyCommandTimeout;
            SdkConfig.RemoteGetRootkeyCommandTimeout = settings.RemoteGetRootkeyCommandTimeout;

            SdkConfig.ConnectDeviceTimeout = settings.ConnectDeviceTimeout;
            SdkConfig.BondDeviceTimeout = settings.BondDeviceTimeout;
            SdkConfig.DeviceInitializationTimeout = settings.DeviceInitializationTimeout;
            SdkConfig.SystemStateEventWaitTimeout = settings.SystemStateEventWaitTimeout;
            SdkConfig.ReconnectDeviceTimeout = settings.ReconnectDeviceTimeout;

            SdkConfig.DeviceBusyTransmitInterval = settings.DeviceBusyTransmitInterval;
            SdkConfig.DeviceBusyTransmitTimeout = settings.DeviceBusyTransmitTimeout;

            SdkConfig.DefaultLockProximity = settings.DefaultLockProximity;
            SdkConfig.DefaultUnlockProximity = settings.DefaultUnlockProximity;
            SdkConfig.DefaultLockTimeout = settings.DefaultLockTimeout;
        }
    }
}
