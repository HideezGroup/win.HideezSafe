using Hideez.ARM;
using Hideez.ISM;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.WorkstationEvents;
using HideezMiddleware.Settings;
using HideezClient.Models;
using HideezClient.Models.Settings;
using HideezClient.Modules.DeviceManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.ServiceModel;
using Hideez.SDK.Communication.Log;
using HideezClient.Modules.Log;

namespace HideezClient.Modules.ActionHandler
{
    /// <summary>
    /// Contains a basic data entry algorithm and helper methods
    /// </summary>
    abstract class InputBase : IInputAlgorithm
    {
        private readonly Logger log = LogManager.GetCurrentClassLogger(nameof(InputBase));
        protected AppInfo currentAppInfo;

        protected readonly IInputHandler inputHandler;
        protected readonly ITemporaryCacheAccount temporaryCacheAccount;
        protected readonly IInputCache inputCache;
        protected readonly ISettingsManager<ApplicationSettings> settingsManager;
        private readonly IWindowsManager windowsManager;
        private readonly IDeviceManager deviceManager;

        protected InputBase(IInputHandler inputHandler, ITemporaryCacheAccount temporaryCacheAccount
            , IInputCache inputCache, ISettingsManager<ApplicationSettings> settingsManager
            , IWindowsManager windowsManager, IDeviceManager deviceManager)
        {
            this.inputHandler = inputHandler;
            this.temporaryCacheAccount = temporaryCacheAccount;
            this.inputCache = inputCache;
            this.settingsManager = settingsManager;
            this.windowsManager = windowsManager;
            this.deviceManager = deviceManager;
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
            if (devicesId == null)
            {
                string message = $"ArgumentNull: {nameof(devicesId)}";
                log.WriteLine(message, LogErrorSeverity.Error);
                Debug.Assert(false, message);
                return;
            }

            if (!devicesId.Any())
            {
                string message = "Vaults id cannot be empty.";
                log.WriteLine(message, LogErrorSeverity.Error);
                Debug.Assert(false, message);
                return;
            }

            if (inputCache.HasCache())
                return;

            try
            {
                await Task.Run(() =>
                {
                    currentAppInfo = AppInfoFactory.GetCurrentAppInfo();
                });

                // Our application window was active during action invocation
                if (currentAppInfo.ProcessName == "HideezClient")
                    throw new HideezWindowSelectedException();

                inputCache.CacheInputField();

                if (await BeforeCondition(devicesId))
                {
                    Account[] accounts = GetAccountsByAppInfoAsync(currentAppInfo, devicesId);
                    accounts = FilterAccounts(accounts, devicesId);

                    if (!accounts.Any()) // No accounts for current application
                    {
                        if (inputCache.HasCache())
                            await inputCache.SetFocusAsync();

                        OnAccountNotFoundError(currentAppInfo, devicesId);
                    }
                    else if (accounts.Length == 1) // Single account for current application
                    {
                        try
                        {
                            await InputAsync(accounts.First());
                        }
                        catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.ChannelNotAuthorized)
                        {
                            log.WriteLine(ex, LogErrorSeverity.Warning);
                            throw new AuthEndedUnexpectedlyException();
                        }
                    }
                    else // Multiple accounts for current application
                    {
                        Account selectedAccount = null;
                        try
                        {
                            selectedAccount = await windowsManager.SelectAccountAsync(accounts, inputCache.WindowHandle);

                            if (selectedAccount != null)
                            {
                                await InputAsync(selectedAccount);
                            }
                            else
                            {
                                log.WriteLine("Account was not selected.");
                            }
                        }
                        catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.ChannelNotAuthorized)
                        {
                            log.WriteLine(ex, LogErrorSeverity.Warning);
                            if (inputCache.HasCache())
                            {
                                await inputCache.SetFocusAsync();
                            }

                            throw new AuthEndedUnexpectedlyException();
                        }
                        catch (SystemException ex) when (ex is OperationCanceledException || ex is TimeoutException)
                        {
                            if (inputCache.HasCache())
                            {
                                await inputCache.SetFocusAsync();
                            }
                            log.WriteLine(ex, LogErrorSeverity.Information);
                        }
                        catch (Exception ex)
                        {
                            Debug.Assert(false, ex.ToString());
                            log.WriteLine(ex);
                        }
                    }
                }
            }
            finally
            {
                inputCache.ClearInputFieldCache();
                currentAppInfo = null;
            }
        }

        private Account[] GetAccountsByAppInfoAsync(AppInfo appInfo, string[] devicesId)
        {
            List<Account> accounts = new List<Account>();

            var devices = deviceManager.Devices.Where(d => d.IsConnected && d.IsAuthorized && d.IsStorageLoaded && devicesId.Contains(d.Id));

            if (devices.Count() == 0)
                throw new AuthEndedUnexpectedlyException();

            foreach (var device in devices)
                accounts.AddRange(device.FindAccountsByApp(appInfo));

            return accounts.ToArray();
        }

        /// <summary>
        /// Try to input string data into Element
        /// Throw Exception if has an error
        /// If is not data for input called method NotFoundAccounts
        /// </summary>
        /// <param name="account">Accounts data for input</param>
        private async Task InputAsync(Account account)
        {
            if (await InputAccountAsync(account))
            {
                OnAccountEntered(currentAppInfo, account);
            }
            else
            {
                OnAccountNotFoundError(currentAppInfo, new[] { account.Device.Id });
            }
        }

        /// <summary>
        /// Show a message or other action when account for target app is missing
        /// </summary>
        /// <param name="appInfo">AppInfo of app for which an account is missing</param>
        /// <param name="devicesId">Devices on which the search was performed</param>
        protected abstract void OnAccountNotFoundError(AppInfo appInfo, string[] devicesId);

        /// <summary>
        /// Called when account was successfully entered into target application
        /// </summary>
        /// <param name="appInfo"></param>
        /// <param name="account"></param>
        protected virtual void OnAccountEntered(AppInfo appInfo, Account account)
        {
        }

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
            return accounts;//.Where(a => devicesId.Contains(a.DeviceId)).ToArray();
        }

        /// <summary>
        /// Checks if data can be input data for this field or application
        /// Condition after get AppInfo, cache input fild and before try to input data
        /// </summary>
        /// <returns>Can be input data for this field or application</returns>
        protected async virtual Task<bool> BeforeCondition(string[] devicesId)
        {
            if (currentAppInfo != null)
            {
                var connectedDevices = deviceManager.Devices.Where(d => d.IsConnected && d.IsInitialized && devicesId.Contains(d.Id)).ToArray();
                foreach (var device in connectedDevices.Where(d => !d.IsAuthorized))
                {
                    await device.InitRemoteAndLoadStorageAsync();
                }

                return connectedDevices.Where(d => d.IsAuthorized).Any();
            }
            else
            {
                return false;
            }
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
        protected void SimulateEnter()
        {
            if (settingsManager.Settings.AddEnterAfterInput)
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
                Account[] pmas = accounts.Where(a => a.Id == cachedAccountPrevInput.Id).ToArray();
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