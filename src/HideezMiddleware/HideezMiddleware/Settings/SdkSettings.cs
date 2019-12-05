﻿using System;

namespace HideezMiddleware.Settings
{
    [Serializable]
    public class SdkSettings : BaseSettings
    {
        public SdkSettings()
        {
            SettingsVersion = new Version(1, 0);
        }

        public SdkSettings(SdkSettings copy)
        {
            if (copy == null)
                return;

            SettingsVersion = (Version)copy.SettingsVersion.Clone();

            MainWorkflowTimeout = copy.MainWorkflowTimeout;
            CredentialProviderLogonTimeout = copy.CredentialProviderLogonTimeout;
            TapProximityUnlockThreshold = copy.TapProximityUnlockThreshold;
            DelayAfterMainWorkflow = copy.DelayAfterMainWorkflow;
            WorkstationUnlockerConnectTimeout = copy.WorkstationUnlockerConnectTimeout;

            DefaultCommandTimeout = copy.DefaultCommandTimeout;
            DefaultRemoteCommandTimeout = copy.DefaultRemoteCommandTimeout;
            VerifyCommandTimeout = copy.VerifyCommandTimeout;
            GetRootKeyCommandTimeout = copy.GetRootKeyCommandTimeout;
            RemoteVerifyCommandTimeout = copy.RemoteVerifyCommandTimeout;

            ConnectDeviceTimeout = copy.ConnectDeviceTimeout;
            DeviceInitializationTimeout = copy.DeviceInitializationTimeout;
            SystemStateEventWaitTimeout = copy.SystemStateEventWaitTimeout;
        }

        [Setting]
        public Version SettingsVersion { get; }

        [Setting]
        public int MainWorkflowTimeout { get; set; } = 120_000;
        [Setting]
        public int CredentialProviderLogonTimeout { get; set; } = 5_000;
        [Setting]
        public int TapProximityUnlockThreshold { get; set; } = -33;
        [Setting]
        public int DelayAfterMainWorkflow { get; set; } = 1500;
        [Setting]
        public int WorkstationUnlockerConnectTimeout { get; set; } = 5_000;


        [Setting]
        public int DefaultCommandTimeout { get; set; } = 5_000;
        [Setting]
        public int DefaultRemoteCommandTimeout { get; set; } = 10_000;
        [Setting]
        public int VerifyCommandTimeout { get; set; } = 10_000;
        [Setting]
        public int GetRootKeyCommandTimeout { get; set; } = 2_000;
        [Setting]
        public int RemoteVerifyCommandTimeout { get; set; } = 10_000;

        [Setting]
        public int ConnectDeviceTimeout { get; set; } = 8_000;
        [Setting]
        public int DeviceInitializationTimeout { get; set; } = 15_000;
        [Setting]
        public int SystemStateEventWaitTimeout { get; set; } = 2_000;


        public override object Clone()
        {
            return new SdkSettings(this);
        }
    }
}
