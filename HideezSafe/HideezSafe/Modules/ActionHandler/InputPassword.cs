﻿using HideezSafe.Models.Settings;
using HideezSafe.Modules.SettingsManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HideezSafe.Modules.Localize;
using Hideez.ISM;
using Hideez.ARS;

namespace HideezSafe.Modules.ActionHandler
{
    /// <summary>
    /// Implement input password algorithm
    /// </summary>
    class InputPassword : InputBase
    {
        public InputPassword(IInputHandler inputHandler, ITemporaryCacheAccount temporaryCacheAccount
                        , IInputCache inputCache, ISettingsManager<ApplicationSettings> settingsManager
                        , IWindowsManager windowsManager)
                        : base(inputHandler, temporaryCacheAccount, inputCache, settingsManager, windowsManager)
        {
        }

        /// <summary>
        /// Get password from service by account deviceId and key and simulate input
        /// </summary>
        /// <param name="account">Account info about the password</param>
        /// <returns>True if found data for password</returns>
        protected override async Task<bool> InputAccountAsync(Account account)
        {
            if (account != null && account.Key > 0)
            {
                string password = await GetPasswordAsync(account);
                if (password != null)
                {
                    await SimulateInput(password);
                    await SimulateEnterAsync();
                    SetCache(account);
                    return true;
                }
            }

            return false;
        }

        private Task<string> GetPasswordAsync(Account account)
        {
            // TODO: get password
            throw new NotImplementedException();
        }

        /// <summary>
        /// Show dialog add new account
        /// </summary>
        /// <param name="appInfo">AppInfo for new account</param>
        /// <param name="devicesId">Devices on which the search was performed</param>
        protected override void NotFoundAccounts(AppInfo appInfo, string[] devicesId)
        {
            throw new PasswordNotFoundException(string.Format(TranslationSource.Instance["Exception.PasswordNotFound"], appInfo.Title), appInfo, devicesId);
        }

        /// <summary>
        /// If enable "Limit password entry to protected fields" and current field is protected password return true
        /// If disabled "Limit password entry to protected fields" return true
        /// </summary>
        protected override bool BeforeCondition()
        {
            var isProtected = ((!settingsManager.Settings.LimitPasswordEntry || settingsManager.Settings.LimitPasswordEntry && inputCache.IsProtectedPasswordField));
            if (!isProtected)
                throw new FieldNotSecureException();
            // Debug.WriteLine($"### InputPassword.InputHandler.IsProtectedPasswordField: {inputHandler.IsProtectedPasswordField}");
            return base.BeforeCondition() && isProtected;
        }

        /// <summary>
        /// Filtering intersect accounts and devices and previous input
        /// </summary>
        /// <param name="accounts">Found accounts on devices</param>
        /// <param name="devicesId">Devices for filtering</param>
        /// <returns>Filtered accounts</returns>
        protected override Account[] FilterAccounts(Account[] accounts, string[] devicesId)
        {
            var filterdAccounts = base.FilterAccounts(accounts, devicesId);
            return FindValueForPreviousInput(filterdAccounts, temporaryCacheAccount.PasswordReqCache.Value);
        }
    }
}
