﻿using Hideez.ARS;
using Hideez.ISM;
using HideezSafe.Models.Settings;
using HideezSafe.Modules.SettingsManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HideezSafe.Modules.ActionHandler
{
    /// <summary>
    /// Contains a basic data entry algorithm and helper methods
    /// </summary>
    abstract class InputBase : IInputAlgorithm
    {
        private AppInfo currentAppInfo;

        protected readonly IInputHandler inputHandler;
        protected readonly ITemporaryCacheAccount temporaryCacheAccount;
        protected readonly IInputCache inputCache;
        protected readonly ISettingsManager<ApplicationSettings> settingsManager;
        private readonly IWindowsManager windowsManager;

        protected InputBase(IInputHandler inputHandler, ITemporaryCacheAccount temporaryCacheAccount
            , IInputCache inputCache, ISettingsManager<ApplicationSettings> settingsManager
            , IWindowsManager windowsManager)
        {
            this.inputHandler = inputHandler;
            this.temporaryCacheAccount = temporaryCacheAccount;
            this.inputCache = inputCache;
            this.settingsManager = settingsManager;
            this.windowsManager = windowsManager;
        }

        /// <summary>
        /// Cache current input field, AppInfo
        /// Get accounts for AppInfo
        /// Filter accounts by divicessId
        /// Filter accounts by previous cached inputs
        /// Call method NotFoundAccounts if not found any
        /// Input data if found one account
        /// Show window to select one account if found more then one
        /// Save Exception in property Exception if was exception during work the method
        /// </summary>
        /// <param name="devicesId">Devices for find account</param>
        public async Task InputAsync(string[] devicesId)
        {
            if (inputCache.HasCache())
                return;

            try
            {
                await Task.Run(() =>
                {
                    currentAppInfo = AppInfoFactory.GetCurrentAppInfo();
                });

                inputCache.CacheInputField();

                if (BeforeCondition())
                {
                    Account[] accounts = null; //TODO: Get accounts for AppInfo
                    // = await serviceProxy.Service.GetPmAccountsByAppInfoAsync(AppInfoConverter.ToDTO(currentAppInfo));
                    accounts = FilterAccounts(accounts, devicesId);

                    if (!accounts.Any()) // No accounts for current application
                    {
                        NotFoundAccounts(currentAppInfo, devicesId);
                    }
                    else if (accounts.Length == 1) // Single account for current application
                    {
                        await InputAsync(accounts.First());
                    }
                    else // Multiple accounts for current application
                    {
                        Account selectedAccount = null;
                        try
                        {
                            selectedAccount = await windowsManager.SelectAccountAsync(accounts);
                        }
                        catch (TaskCanceledException)
                        {
                        }

                        if (selectedAccount != null)
                        {
                            await InputAsync(selectedAccount);
                        }
                    }
                }
            }
            finally
            {
                inputCache.ClearInputFieldCache();
            }
        }

        /// <summary>
        /// Try to input string data into Element
        /// Throw Exception if has an error
        /// If is not data for input called method NotFoundAccounts
        /// </summary>
        /// <param name="account">Accounts data for input</param>
        private async Task InputAsync(Account account)
        {
            if (!await InputAccountAsync(account))
            {
                NotFoundAccounts(currentAppInfo, new[] { account.DeviceId });
            }
        }

        /// <summary>
        /// Show a message or other action if no any accounts found
        /// </summary>
        /// <param name="appInfo">AppInfo for not found account</param>
        /// <param name="devicesId">Devices on which the search was performed</param>
        protected abstract void NotFoundAccounts(AppInfo appInfo, string[] devicesId);

        /// <summary>
        /// Processing input data
        /// And Try to input string data into Element
        /// Throw Exception if has an error
        /// </summary>
        /// <param name="account">AppInfo for found account</param>
        /// <returns>True if found data for input</returns>
        protected abstract Task<bool> InputAccountAsync(Account account);

        /// <summary>
        /// Filter accounts before input or select any
        /// Base filtering intersect accounts and devices
        /// </summary>
        /// <param name="accounts">Found accounts on devices</param>
        /// <returns>Filtered accounts</returns>
        protected virtual Account[] FilterAccounts(Account[] accounts, string[] devicesId)
        {
            return accounts.Where(a => devicesId.Contains(a.DeviceId)).ToArray();
        }

        /// <summary>
        /// Checks if data can be input data for this field or application
        /// Condition after get AppInfo, cache input fild and before try to input data
        /// </summary>
        /// <returns>Can be input data for this field or application</returns>
        protected virtual bool BeforeCondition()
        {
            return currentAppInfo != null;
        }

        /// <summary>
        /// Simulate input string into cached file
        /// </summary>
        /// <param name="text">String for input</param>
        protected async Task SimulateInput(string text)
        {
            InputApp inputApp = InputHelper.GetInputApp(currentAppInfo);
            await inputHandler.SimulateKeyboardInputAsync(inputCache, text, inputApp);
        }

        /// <summary>
        /// Simulate press key Enter
        /// </summary>
        protected async Task SimulateEnterAsync()
        {
            ApplicationSettings settings = await settingsManager.GetSettingsAsync();
            if (settings.AddEnterAfterInput)
                inputHandler.SimulateEnterKeyPress();
        }

        /// <summary>
        /// Find accounts by cached account of previous input
        /// </summary>
        /// <param name="accounts">Found accounts on devices</param>
        /// <param name="cachedAccountPrevInput">Cached account of previous input</param>
        /// <returns></returns>
        protected Account[] FindValueForPreviousInput(Account[] accounts, Account cachedAccountPrevInput)
        {
            if (cachedAccountPrevInput != null)
            {
                Account[] pmas = accounts.Where(a => a.Key == cachedAccountPrevInput.Key && a.DeviceId.Equals(cachedAccountPrevInput.DeviceId)).ToArray();
                if (pmas.Length > 0)
                {
                    return pmas;
                }
            }

            return accounts;
        }

        /// <summary>
        /// Set account to cache
        /// </summary>
        /// <param name="account">Account for cache</param>
        protected void SetCache(Account account)
        {
            temporaryCacheAccount.PasswordReqCache.Value = account;
            temporaryCacheAccount.OtpReqCache.Value = account;
        }
    }
}