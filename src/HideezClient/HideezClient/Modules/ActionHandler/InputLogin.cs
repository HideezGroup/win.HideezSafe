﻿using Hideez.ARM;
using Hideez.ISM;
using HideezMiddleware.Settings;
using HideezClient.Models;
using HideezClient.Models.Settings;
using HideezClient.Modules.DeviceManager;
using HideezClient.Modules.Localize;
using System.Threading.Tasks;
using Hideez.SDK.Communication;
using System;
using System.Linq;
using HideezMiddleware.IPC.DTO;

namespace HideezClient.Modules.ActionHandler
{
    /// <summary>
    /// Implement input Login algothoritm
    /// </summary>
    class InputLogin : InputBase
    {
        readonly IEventPublisher _eventPublisher;

        public InputLogin(IInputHandler inputHandler, ITemporaryCacheAccount temporaryCacheAccount
                        , IInputCache inputCache, ISettingsManager<ApplicationSettings> settingsManager
                        , IWindowsManager windowsManager, IDeviceManager deviceManager
                        , IEventPublisher eventPublisher)
                        : base(inputHandler, temporaryCacheAccount, inputCache, settingsManager, windowsManager, deviceManager)
        {
            _eventPublisher = eventPublisher;
        }

        /// <summary>
        /// If account has login simulate input
        /// </summary>
        /// <param name="account">Account info about the login</param>
        /// <returns>True if PmAccountDTO has login</returns>
        protected override async Task<bool> InputAccountAsync(Account account)
        {
            if (account != null && !string.IsNullOrWhiteSpace(account.Login))
            {
                await SimulateInput(account.Login);
                SetCache(account);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Filtering intersect accounts and devices
        /// </summary>
        /// <param name="accounts">Found accounts on devices</param>
        /// <param name="devicesId">Devices for filtering</param>
        /// <returns>Filtered accounts</returns>
        protected override Account[] FilterAccounts(Account[] accounts, string[] devicesId)
        {
            return base.FilterAccounts(accounts, devicesId).Where(a => !string.IsNullOrWhiteSpace(a.Login)).ToArray();
        }

        /// <summary>
        /// Show dialog add new account
        /// </summary>
        /// <param name="appInfo">AppInfo for new account</param>
        /// <param name="devicesId">Devices on which the search was performed</param>
        protected override void OnAccountNotFoundError(AppInfo appInfo, string[] devicesId)
        {
            throw new LoginNotFoundException(string.Format(TranslationSource.Instance["Exception.LoginNotFound"], appInfo.Title), appInfo, devicesId);
        }

        protected override async void OnAccountEntered(AppInfo appInfo, Account account)
        {
            base.OnAccountEntered(appInfo, account);

            await _eventPublisher.PublishEventAsync(new WorkstationEventDTO
            {
                Id = Guid.NewGuid().ToString(),
                Date = DateTime.UtcNow,
                AccountLogin = account.Login,
                AccountName = account.Name,
                DeviceId = account.Device.SerialNo,
                EventId = (int)WorkstationEventType.CredentialsUsed_Login,
                Note = appInfo.Title,
                Severity = (int)WorkstationEventSeverity.Info,
            });
        }
    }
}