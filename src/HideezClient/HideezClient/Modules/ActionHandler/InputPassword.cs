using HideezClient.Models.Settings;
using System.Threading.Tasks;
using HideezMiddleware.Localize;
using Hideez.ISM;
using HideezClient.Models;
using HideezClient.Modules.DeviceManager;
using Hideez.ARM;
using HideezMiddleware.Settings;
using Hideez.SDK.Communication;
using System;
using HideezMiddleware.IPC.DTO;

namespace HideezClient.Modules.ActionHandler
{
    /// <summary>
    /// Implement input password algorithm
    /// </summary>
    class InputPassword : InputBase
    {
        readonly IEventPublisher _eventPublisher;

        public InputPassword(IInputHandler inputHandler, ITemporaryCacheAccount temporaryCacheAccount
                        , IInputCache inputCache, ISettingsManager<ApplicationSettings> settingsManager
                        , IWindowsManager windowsManager, IDeviceManager deviceManager
                        , IEventPublisher eventPublisher)
                        : base(inputHandler, temporaryCacheAccount, inputCache, settingsManager, windowsManager, deviceManager)
        {
            _eventPublisher = eventPublisher;
        }

        /// <summary>
        /// Get password from service by account deviceId and key and simulate input
        /// </summary>
        /// <param name="account">Account info about the password</param>
        /// <returns>True if found data for password</returns>
        protected override async Task<bool> InputAccountAsync(Account account)
        {
            if (account != null)
            {
                string password = await account?.TryGetPasswordAsync();
                if (!string.IsNullOrEmpty(password))
                {
                    await SimulateInput(password);
                    SimulateEnter();
                    SetCache(account);
                    temporaryCacheAccount.PasswordReqCache.Clear();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Show dialog add new account
        /// </summary>
        /// <param name="appInfo">AppInfo for new account</param>
        /// <param name="devicesId">Devices on which the search was performed</param>
        protected override void OnAccountNotFoundError(AppInfo appInfo, string[] devicesId)
        {
            throw new PasswordNotFoundException(string.Format(TranslationSource.Instance["Exception.PasswordNotFound"], appInfo.Title), appInfo, devicesId);
        }

        /// <summary>
        /// If enable "Enter passwords only to secure fields" and current field is protected password return true
        /// If disabled "Enter passwords only to secure fields" return true
        /// </summary>
        protected async override Task<bool> BeforeCondition(string[] devicesId)
        {
            if (settingsManager.Settings.LimitPasswordEntry && !inputCache.IsProtectedPasswordField)
            { 
                throw new FieldNotSecureException();
            }

            // Debug.WriteLine($"### InputPassword.InputHandler.IsProtectedPasswordField: {inputHandler.IsProtectedPasswordField}");
            return await base.BeforeCondition(devicesId);
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
                EventId = (int)WorkstationEventType.CredentialsUsed_Password,
                Note = appInfo.Title,
                Severity = (int)WorkstationEventSeverity.Info,
            });
        }
    }
}
