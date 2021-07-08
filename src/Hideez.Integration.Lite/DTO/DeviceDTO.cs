using Hideez.Integration.Lite.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Runtime.Serialization;

namespace Hideez.Integration.Lite.DTO
{
    [DataContract]
    public class DeviceDTO
    {
        public DeviceDTO()
        {
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
        public bool IsBoot { get; set; }

        [DataMember]
        public bool IsRemote { get; set; }

        [DataMember]
        public sbyte Battery { get; set; }

        [DataMember]
        [JsonConverter(typeof(VersionConverter))]
        public Version FirmwareVersion { get; set; }

        [DataMember]
        [JsonConverter(typeof(VersionConverter))]
        public Version BootloaderVersion { get; set; }

        [DataMember]
        public bool IsInitialized { get; set; }

        [DataMember]
        public bool IsAuthorized { get; set; }

        [DataMember]
        public uint StorageTotalSize { get; set; }

        [DataMember]
        public uint StorageFreeSize { get; set; }

        [DataMember]
        public int MinPinLength { get; set; }

        [DataMember]
        public int PinAttemptsRemain { get; set; }

        [DataMember]
        public bool IsCanUnlock { get; set; }

        [DataMember]
        public int UnlockAttemptsRemain { get; set; }

        [DataMember]
        public HwVaultConnectionState HwVaultConnectionState { get; set; }

        [DataMember]
        public double Proximity { get; set; }

        [DataMember]
        public bool CanLockPyProximity { get; set; }

        [DataMember]
        public byte ConnectionType { get; set; }
    }
}
