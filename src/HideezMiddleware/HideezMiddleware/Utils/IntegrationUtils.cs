using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using System;
using Hideez.Integration.Lite.DTO;
using Hideez.Integration.Lite.Enums;
using SdkHwVaultConnectionState = Hideez.SDK.Communication.HES.DTO.HwVaultConnectionState;
using SdkButtonPressCode = Hideez.SDK.Communication.ButtonPressCode;

namespace HideezMiddleware.Utils
{
    static class IntegrationUtils
    {
        public static DeviceDTO Initialize(this DeviceDTO deviceDTO, IDevice device)
        {
            if(device != null)
            {
                deviceDTO.SnapshotTime = DateTime.UtcNow;
                deviceDTO.Id = device.Id;
                deviceDTO.NotificationsId = device.DeviceConnection.Connection.ConnectionId.Id;
                deviceDTO.Name = device.Name;
                deviceDTO.IsConnected = device.IsConnected;
                deviceDTO.IsBoot = device.IsBoot;
                deviceDTO.IsRemote = device is IRemoteDeviceProxy;
                deviceDTO.Battery = device.Battery;
                deviceDTO.SerialNo = device.SerialNo;
                deviceDTO.ChannelNo = device.ChannelNo;
                deviceDTO.Mac = device.Mac;
                deviceDTO.FirmwareVersion = device.FirmwareVersion;
                deviceDTO.BootloaderVersion = device.BootloaderVersion;
                deviceDTO.IsInitialized = device.IsInitialized;
                deviceDTO.StorageTotalSize = device.StorageTotalSize;
                deviceDTO.StorageFreeSize = device.StorageFreeSize;
                deviceDTO.IsAuthorized = device.IsAuthorized;
                deviceDTO.MinPinLength = device.MinPinLength;
                deviceDTO.PinAttemptsRemain = device.PinAttemptsRemain;
                deviceDTO.IsCanUnlock = device.IsCanUnlock;
                deviceDTO.UnlockAttemptsRemain = device.UnlockAttemptsRemain;
                deviceDTO.HwVaultConnectionState = (byte)device.GetUserProperty<SdkHwVaultConnectionState>(CustomProperties.HW_CONNECTION_STATE_PROP);
                deviceDTO.Proximity = device.Proximity;
                deviceDTO.CanLockPyProximity = device.GetUserProperty<bool>(WorkstationLockProcessor.PROX_LOCK_ENABLED_PROP);
                deviceDTO.OwnerName = device.GetUserProperty<string>(DeviceCustomProperties.OWNER_NAME_PROP) ?? string.Empty;
                deviceDTO.OwnerEmail = device.GetUserProperty<string>(DeviceCustomProperties.OWNER_EMAIL_PROP) ?? string.Empty;
                deviceDTO.ConnectionType = device.DeviceConnection.Connection.ConnectionId.IdProvider;
            }
            
            return deviceDTO;
        }

        public static ButtonPressCode ConvertButtonPressCodeFromSdk(SdkButtonPressCode sdkButtonPressCode)
        {
            ButtonPressCode newButtonPressCode = ButtonPressCode.None;
            switch (sdkButtonPressCode)
            {
                case SdkButtonPressCode.Single:
                    newButtonPressCode = ButtonPressCode.Single;
                    break;
                case SdkButtonPressCode.Double:
                    newButtonPressCode = ButtonPressCode.Double;
                    break;
                case SdkButtonPressCode.Triple:
                    newButtonPressCode = ButtonPressCode.Triple;
                    break;
                case SdkButtonPressCode.Quad:
                    newButtonPressCode = ButtonPressCode.Quad;
                    break;
                case SdkButtonPressCode.Penta:
                    newButtonPressCode = ButtonPressCode.Penta;
                    break;
                case SdkButtonPressCode.Hexa:
                    newButtonPressCode = ButtonPressCode.Hexa;
                    break;
                case SdkButtonPressCode.Hepta:
                    newButtonPressCode = ButtonPressCode.Hepta;
                    break;
                case SdkButtonPressCode.Octa:
                    newButtonPressCode = ButtonPressCode.Octa;
                    break;
                case SdkButtonPressCode.Multi:
                    newButtonPressCode = ButtonPressCode.Multi;
                    break;
                case SdkButtonPressCode.Long:
                    newButtonPressCode = ButtonPressCode.Long;
                    break;
                case SdkButtonPressCode.SuperLong:
                    newButtonPressCode = ButtonPressCode.SuperLong;
                    break;
            }

            return newButtonPressCode;
        }
    }
}
