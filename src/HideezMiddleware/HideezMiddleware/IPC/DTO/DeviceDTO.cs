using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Runtime.Serialization;

namespace HideezMiddleware.IPC.DTO
{
    [DataContract]
    public class DeviceDTO
    {
        public DeviceDTO() 
        { 
        }

        public DeviceDTO(IDevice device)
        {
            SnapshotTime = DateTime.UtcNow;
            Id = device.Id;
            // NotificationsId is consistent with what is used in service as id for notifications sent to client interfaces
            NotificationsId = device.DeviceConnection.Connection.ConnectionId.Id; 
            Name = device.Name;
            IsConnected = device.IsConnected;
            IsBoot = device.IsBoot;
            IsRemote = device is IRemoteDeviceProxy;
            Battery = device.Battery;
            SerialNo = device.SerialNo;
            ChannelNo = device.ChannelNo;
            Mac = device.Mac;
            FirmwareVersion = device.FirmwareVersion;
            BootloaderVersion = device.BootloaderVersion;
            IsInitialized = device.IsInitialized;
            StorageTotalSize = device.StorageTotalSize;
            StorageFreeSize = device.StorageFreeSize;
            IsAuthorized = device.IsAuthorized;
            MinPinLength = device.MinPinLength;
            PinAttemptsRemain = device.PinAttemptsRemain;
            IsCanUnlock = device.IsCanUnlock;
            UnlockAttemptsRemain = device.UnlockAttemptsRemain;
            HwVaultConnectionState = device.GetUserProperty<HwVaultConnectionState>(CustomProperties.HW_CONNECTION_STATE_PROP);
            Proximity = device.Proximity;
            CanLockPyProximity = device.GetUserProperty<bool>(WorkstationLockProcessor.PROX_LOCK_ENABLED_PROP);
            OwnerName = device.GetUserProperty<string>(DeviceCustomProperties.OWNER_NAME_PROP) ?? string.Empty;
            OwnerEmail = device.GetUserProperty<string>(DeviceCustomProperties.OWNER_EMAIL_PROP) ?? string.Empty;
            ConnectionType = device.DeviceConnection.Connection.ConnectionId.IdProvider;
        }

        [DataMember]
        public DateTime SnapshotTime { get; set; }

        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string NotificationsId { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string SerialNo { get; set; }
        
        [DataMember]
        public byte ChannelNo { get; set; }

        [DataMember]
        public string Mac { get; set; }

        [DataMember]
        public string OwnerName { get; set; }

        [DataMember]
        public string OwnerEmail { get; set; }

        [DataMember]
        public bool IsConnected { get; set; }

        [DataMember]
        public bool IsBoot { get; private set; }

        [DataMember]
        public bool IsRemote { get; private set; }

        [DataMember]
        public sbyte Battery { get; set; }

        [DataMember]
        [JsonConverter(typeof(VersionConverter))] 
        public Version FirmwareVersion { get; private set; }

        [DataMember]
        [JsonConverter(typeof(VersionConverter))] 
        public Version BootloaderVersion { get; private set; }

        [DataMember]
        public bool IsInitialized { get; private set; }

        [DataMember]
        public bool IsAuthorized { get; private set; }

        [DataMember]
        public uint StorageTotalSize { get; private set; }

        [DataMember]
        public uint StorageFreeSize { get; private set; }

        [DataMember]
        public int MinPinLength { get; private set; }

        [DataMember]
        public int PinAttemptsRemain { get; private set; }

        [DataMember]
        public bool IsCanUnlock { get; private set; }

        [DataMember]
        public int UnlockAttemptsRemain { get; private set; }

        [DataMember]
        public HwVaultConnectionState HwVaultConnectionState { get; private set; }

        [DataMember]
        public double Proximity { get; private set; }
        
        [DataMember]
        public bool CanLockPyProximity { get; private set; }

        [DataMember]
        public byte ConnectionType { get; private set; }
    }
}
