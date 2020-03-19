﻿using Hideez.SDK.Communication;
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

            SdkConfig.DefaultCommandTimeout = settings.DefaultCommandTimeout;
            SdkConfig.DefaultRemoteCommandTimeout = settings.DefaultRemoteCommandTimeout;
            SdkConfig.VerifyCommandTimeout = settings.VerifyCommandTimeout;
            SdkConfig.GetRootKeyCommandTimeout = settings.GetRootKeyCommandTimeout;
            SdkConfig.RemoteVerifyCommandTimeout = settings.RemoteVerifyCommandTimeout;

            SdkConfig.ConnectDeviceTimeout = settings.ConnectDeviceTimeout;
            SdkConfig.DeviceInitializationTimeout = settings.DeviceInitializationTimeout;
            SdkConfig.SystemStateEventWaitTimeout = settings.SystemStateEventWaitTimeout;

            SdkConfig.DeviceBusyTransmitInterval = settings.DeviceBusyTransmitInterval;
            SdkConfig.DeviceBusyTransmitTimeout = settings.DeviceBusyTransmitTimeout;
            SdkConfig.HesRequestTimeout = settings.HesRequestTimeout;
        }
    }
}
