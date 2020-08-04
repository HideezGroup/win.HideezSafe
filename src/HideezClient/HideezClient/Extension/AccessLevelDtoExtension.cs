﻿using Hideez.SDK.Communication.Device;
using HideezMiddleware.IPC.DTO;

namespace HideezClient.Extension
{
    static class AccessLevelDTOExtension
    {
        public static AccessLevel ToAccessLevel(this AccessLevelDTO dto)
        {
            return new AccessLevel(
                dto.IsLinkRequired,
                dto.IsNewPinRequired,
                dto.IsMasterKeyRequired,
                dto.IsPinRequired,
                dto.IsButtonRequired,
                dto.IsLocked);
        }
    }
}
