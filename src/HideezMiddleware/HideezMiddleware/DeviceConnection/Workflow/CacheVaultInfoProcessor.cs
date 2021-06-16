using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using HideezMiddleware.Local;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.Workflow
{
    public class CacheVaultInfoProcessor: Logger, ICacheVaultInfoProcessor
    {
        readonly ILocalDeviceInfoCache _localDeviceInfoCache;

        public CacheVaultInfoProcessor(ILocalDeviceInfoCache localDeviceInfoCache, ILog log)
            : base(nameof(CacheVaultInfoProcessor), log)
        {
            _localDeviceInfoCache = localDeviceInfoCache;
        }

        public void CacheAndUpdateVaultOwner(ref IDevice device, HwVaultInfoFromHesDto dto, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(dto.VaultMac))
                LoadLocalVaultOwner(ref device);
            else
            {
                UpdateVaultOwner(ref device, dto.OwnerName, dto.OwnerEmail);
                CacheVaultInfoAsync(dto);
            }
        }

        void CacheVaultInfoAsync(HwVaultInfoFromHesDto dto)
        {
            if (_localDeviceInfoCache != null)
            {
                Task.Run(() =>
                {
                    var info = new LocalDeviceInfo
                    {
                        Mac = dto.VaultMac,
                        SerialNo = dto.VaultSerialNo,
                        OwnerName = dto.OwnerName,
                        OwnerEmail = dto.OwnerEmail,
                    };

                    _localDeviceInfoCache.SaveLocalInfo(info);
                }).ConfigureAwait(false);
            }
            else
                WriteLine("Failed to cache info: Local info cache not available");
        }

        void LoadLocalVaultOwner(ref IDevice device)
        {
            if (_localDeviceInfoCache != null)
            {
                var localDeviceInfo = _localDeviceInfoCache.GetLocalInfo(device.Mac);
                if (localDeviceInfo != null)
                    UpdateVaultOwner(ref device, localDeviceInfo.OwnerName, localDeviceInfo.OwnerEmail);
                else
                    WriteLine("Failed to load info: Local vault info not found");
            }
            else
                WriteLine("Failed to load info: Local info cache not available");
        }

        void UpdateVaultOwner(ref IDevice device, string ownerName, string ownerEmail)
        {
            if (!string.IsNullOrWhiteSpace(ownerName))
                device.SetUserProperty(DeviceCustomProperties.OWNER_NAME_PROP, ownerName);

            if (!string.IsNullOrWhiteSpace(ownerEmail))
                device.SetUserProperty(DeviceCustomProperties.OWNER_EMAIL_PROP, ownerEmail);
        }
    }
}
