﻿using Hideez.SDK.Communication.PasswordManager;
using HideezSafe.Utilities;
using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HideezSafe.Models
{
    public class Account
    {
        private readonly Device device;
        private readonly AccountRecord accountRecord;

        public Account(Device device, AccountRecord accountRecord)
        {
            this.device = device;
            this.accountRecord = accountRecord;
        }

        public string Id => $"{device.Id}:{accountRecord.Key}";
        public Device Device => device;
        public string Name => accountRecord.Name;

        public string[] Apps => AccountUtility.Split(accountRecord.Apps);
        public string[] Domains => AccountUtility.Split(accountRecord.Urls);

        public string Login => accountRecord.Login;
        public bool HasOtp => accountRecord.HasOtp;

        public async Task<string> TryGetPasswordAsync()
        {
            string password = null;

            try
            {
                if (device.IsConnected && device.IsInitialized)
                {
                    return await device.PasswordManager.GetPasswordAsync(accountRecord.Key);
                }
            }
            catch { }

            return password;
        }

        public async Task<string> TryGetOptSecretAsync()
        {
            throw new NotImplementedException("Not implemented Otp secret.");
            string otpSecret = null;
            try
            {
                if (device.IsConnected && device.IsInitialized && HasOtp)
                {
                    // TODO: Implemented Otp
                }
            }
            catch { }

            return otpSecret;
        }
    }
}
