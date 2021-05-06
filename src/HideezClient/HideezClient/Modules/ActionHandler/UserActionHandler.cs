using System;
using HideezClient.Messages;
using System.Threading.Tasks;
using HideezMiddleware.Localize;
using GalaSoft.MvvmLight.Messaging;
using HideezClient.Models;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication;
using System.Threading;
using Hideez.ARM;
using HideezClient.Models.Settings;
using HideezMiddleware.Settings;
using HideezClient.Modules.HotkeyManager;
using Meta.Lib.Modules.PubSub;
using HideezClient.Messages.Hotkeys;

namespace HideezClient.Modules.ActionHandler
{
    /// <summary>
    /// Responsible for receiving user action requests and routing them for execution
    /// </summary>
    class UserActionHandler : Logger
    {
        private readonly IActiveDevice _activeDevice;
        private readonly InputLogin _inputLogin;
        private readonly InputPassword _inputPassword;
        private readonly InputOtp _inputOtp;
        private int _isPerformingActionLock = 0;
        private const int DELAY_BEFORE_NEXT_ACTION = 500; // Delay after user action is finished but before next one will be handled
        private readonly ISettingsManager<ApplicationSettings> _appSettingsManager;
        private readonly IHotkeyManager _hotkeyManager;
        private readonly IWindowsManager _windowsManager;
        private readonly IMetaPubSub _metaMessenger;

        public UserActionHandler(
            IMetaPubSub metaMessenger,
            IActiveDevice activeDevice,
            InputLogin inputLogin,
            InputPassword inputPassword,
            InputOtp inputOtp,
            ISettingsManager<ApplicationSettings> appSettingsManager,
            IHotkeyManager hotkeyManager,
            IWindowsManager windowsManager,
            ILog log)
            : base(nameof(UserActionHandler), log)
        {
            _metaMessenger = metaMessenger;
            _activeDevice = activeDevice;
            _inputLogin = inputLogin;
            _inputPassword = inputPassword;
            _inputOtp = inputOtp;
            _appSettingsManager = appSettingsManager;
            _hotkeyManager = hotkeyManager;
            _windowsManager = windowsManager;

            _metaMessenger.Subscribe<HotkeyPressedMessage>(HotkeyPressedMessageHandler);
            _metaMessenger.Subscribe<ButtonPressedMessage>(ButtonPressedMessageHandler);
        }

        // Messages from Messenger are events. async void is fine in this case.
        Task ButtonPressedMessageHandler(ButtonPressedMessage message)
        {
            return Task.Run(async () =>
            {
                WriteLine($"Handling button pressed message ({message.Action.ToString()}, {message.Code.ToString()})");

                if (_activeDevice.Device?.Id == message.DeviceId)
                {
                    if (_activeDevice.Device.OtherConnections > 0)
                    {
                        string info = TranslationSource.Instance["UserAction.ButtonDisabled.ToManyOtherConnections"];
                        var deviceName = _activeDevice.Device.Name;
                        var otherConnections = _activeDevice.Device.OtherConnections;
                        var hotkey = await _hotkeyManager.GetEnabledKeystrokeForAction(message.Action);
                        if (string.IsNullOrWhiteSpace(hotkey))
                            hotkey = TranslationSource.Instance["Hotkey.NotSet"];
                        var localizedAction = TranslationSource.Instance[$"Enum.UserAction.{message.Action.ToString()}"].ToLowerInvariant();
                        info = string.Format(info, deviceName, otherConnections, hotkey, localizedAction);
                        var msgOptions = new NotificationOptions { CloseTimeout = NotificationOptions.LongTimeout };
                        await _metaMessenger.Publish(new ShowInfoNotificationMessage(info, options: msgOptions, notificationId: _activeDevice.Device.Mac));
                    }
                    else if (!_activeDevice.Device.IsLoadingStorage)
                        await HandleButtonActionAsync(message.DeviceId, message.Action, message.Code);
                }
                else
                {
                    string warn = string.Format(TranslationSource.Instance["UserAction.DeviceIsNotActive"], message.DeviceId);
                    await _metaMessenger.Publish(new ShowWarningNotificationMessage(warn, notificationId: _activeDevice.Device?.Mac));
                    WriteLine(warn, LogErrorSeverity.Warning);
                }
            });
        }

        Task HotkeyPressedMessageHandler(HotkeyPressedMessage message)
        {
            return Task.Run(async () =>
            {
                WriteLine($"Handling hotkey pressed message ({message.Hotkey}, {message.Action.ToString()})");

                if (_activeDevice.Device == null)
                {
                    string warning = TranslationSource.Instance["NoConnectedDevices"];
                    await _metaMessenger.Publish(new ShowWarningNotificationMessage(warning));
                    WriteLine(warning, LogErrorSeverity.Warning);
                    return;
                }
                else if(!_activeDevice.Device.IsLoadingStorage)
                    await HandleHotkeyActionAsync(_activeDevice.Device.Id, message.Action, message.Hotkey);
            });
        }

        async Task HandleHotkeyActionAsync(string activeDeviceId, UserAction action, string hotkey)
        {
            try
            {
                await HandleActionAsync(activeDeviceId, action);
            }
            catch (HideezWindowSelectedException)
            {
                var localizedAction = $"{TranslationSource.Instance["UserAction.PressHotkey"]} {hotkey}";
                var message = TranslationSource.Instance[$"UserAction.HideezWindowSelected.{action.ToString()}"];
                message = string.Format(message, Environment.NewLine, localizedAction);
                var msgOptions = new NotificationOptions { CloseTimeout = NotificationOptions.LongTimeout };
                await _metaMessenger.Publish(new ShowInfoNotificationMessage(message, options: msgOptions, notificationId: _activeDevice.Device?.Mac));
            }
            catch (ActionNotSupportedException)
            {
                var localizedAction = TranslationSource.Instance[$"Enum.UserAction.{action.ToString()}"].ToLowerInvariant();
                await _metaMessenger.Publish(new ShowInfoNotificationMessage(string.Format(TranslationSource.Instance["UserAction.NotSupportedPart"], localizedAction, Environment.NewLine), notificationId: _activeDevice.Device?.Mac));
            }
            catch (Exception ex)
            {
                await _metaMessenger.Publish(new ShowErrorNotificationMessage(ex.Message, notificationId: _activeDevice.Device?.Mac));
                WriteLine(ex, LogErrorSeverity.Error);
            }
        }

        async Task HandleButtonActionAsync(string senderDeviceId, UserAction action, ButtonPressCode code)
        {
            try
            {
                await HandleActionAsync(senderDeviceId, action);
            }
            catch (HideezWindowSelectedException)
            {
                var localizedAction = $"{TranslationSource.Instance["UserAction.PressButton"]} {TranslationSource.Instance[$"Enum.ButtonPressCode.{code.ToString()}"]}";
                var message = TranslationSource.Instance[$"UserAction.HideezWindowSelected.{action.ToString()}"];
                message = string.Format(message, Environment.NewLine, localizedAction);
                var msgOptions = new NotificationOptions { CloseTimeout = NotificationOptions.LongTimeout };
                await _metaMessenger.Publish(new ShowInfoNotificationMessage(message, options: msgOptions, notificationId: _activeDevice.Device?.Mac));
            }
            catch (ActionNotSupportedException)
            {
                var localizedAction = TranslationSource.Instance[$"Enum.UserAction.{action.ToString()}"].ToLowerInvariant();
                await _metaMessenger.Publish(new ShowInfoNotificationMessage(string.Format(TranslationSource.Instance["UserAction.NotSupportedPart"], localizedAction, Environment.NewLine), notificationId: _activeDevice.Device?.Mac));
            }
            catch (Exception ex)
            {
                await _metaMessenger.Publish(new ShowErrorNotificationMessage(ex.Message, notificationId: _activeDevice.Device?.Mac));
                WriteLine(ex, LogErrorSeverity.Error);
            }
        }

        async Task HandleActionAsync(string deviceId, UserAction action)
        {
            if (Interlocked.CompareExchange(ref _isPerformingActionLock, 1, 0) == 0)
            {
                WriteLine("User action lock engaged");
                try
                {
                    switch (action)
                    {
                        case UserAction.InputLogin:
                            await HandleInputActionAsync(deviceId, _inputLogin);
                            break;
                        case UserAction.InputPassword:
                            await HandleInputActionAsync(deviceId, _inputPassword);
                            break;
                        case UserAction.InputOtp:
                            await HandleInputActionAsync(deviceId, _inputOtp);
                            break;
                        case UserAction.AddAccount:
                            await OnAddNewAccount(deviceId);
                            break;
                        case UserAction.LockWorkstation:
                            await _metaMessenger.PublishOnServer(new HideezMiddleware.IPC.Messages.LockWorkstationMessage());
                            break;
                        default:
                            throw new NotImplementedException($"\"{action.ToString()}\" action is not implemented in UserActionHandler.");
                    }
                }
                finally
                {
                    await Task.Delay(DELAY_BEFORE_NEXT_ACTION);
                    Interlocked.Exchange(ref _isPerformingActionLock, 0);
                    WriteLine("User action lock lifted");
                }
            }
            else
                WriteLine($"{action.ToString()} canceled because another action is in progress");
        }

        async Task HandleInputActionAsync(string deviceId, IInputAlgorithm inputAlgorithm)
        {
            try
            {
                await inputAlgorithm.InputAsync(new[] { deviceId });
            }
            catch (OtpNotFoundException ex)
            {
                await _metaMessenger.Publish(new ShowWarningNotificationMessage(ex.Message, notificationId: _activeDevice.Device?.Mac));
                WriteLine(ex.Message, LogErrorSeverity.Warning);
            }
            catch (AccountException ex) when (ex is LoginNotFoundException || ex is PasswordNotFoundException)
            {
                if (_appSettingsManager.Settings.UseSimplifiedUI)
                {
                    await _metaMessenger.Publish(new ShowWarningNotificationMessage(ex.Message, notificationId: _activeDevice.Device?.Mac));
                    WriteLine(ex.Message, LogErrorSeverity.Warning);
                }
                else if (_appSettingsManager.Settings.AutoCreateAccountIfNotFound)
                {
                    await OnAddNewAccount(deviceId);
                }
                else
                {
                    var appInfo = AppInfoFactory.GetCurrentAppInfo();
                    WriteLine(ex.Message, LogErrorSeverity.Warning);
                    var createNewAccount = await _windowsManager.ShowAccountNotFoundAsync(ex.Message);

                    if (createNewAccount)
                        await OnAddNewAccount(deviceId, appInfo);
                }
            }
            catch (FieldNotSecureException) // Assume that precondition failed because field is not secure
            {
                string message = TranslationSource.Instance["Exception.FieldNotSecure"];
                await _metaMessenger.Publish(new ShowWarningNotificationMessage(message, notificationId: _activeDevice.Device?.Mac));
                WriteLine(message, LogErrorSeverity.Warning);
            }
            catch (AuthEndedUnexpectedlyException)
            {
                var message = TranslationSource.Instance["Exception.AuthEndedUnexpectedly"];
                await _metaMessenger.Publish(new ShowWarningNotificationMessage(message, notificationId: _activeDevice.Device?.Mac));
                WriteLine(message, LogErrorSeverity.Warning);
            }
        }

        async Task OnAddNewAccount(string deviceId, AppInfo appInfo = null)
        {
            if (_appSettingsManager.Settings.UseSimplifiedUI)
                throw new ActionNotSupportedException();

            if (appInfo == null)
                appInfo = AppInfoFactory.GetCurrentAppInfo();

            if (appInfo.ProcessName == "HideezClient")
                throw new HideezWindowSelectedException();

            if (_activeDevice.Device.IsLoadingStorage)
                return;

            if (!_activeDevice.Device.IsAuthorized || !_activeDevice.Device.IsStorageLoaded)
            {
                await _activeDevice.Device.InitRemoteAndLoadStorageAsync();
            }
            
            if (_activeDevice.Device.IsAuthorized && _activeDevice.Device.IsStorageLoaded)
            {
                await _metaMessenger.Publish(new ShowInfoNotificationMessage(string.Format(TranslationSource.Instance["UserAction.Notification.CreatingNewAccount"], appInfo.Title), notificationId: _activeDevice.Device?.Mac));
                await _metaMessenger.Publish(new ShowActivateMainWindowMessage());
                await _metaMessenger.Publish(new OpenPasswordManagerMessage(deviceId));
                await _metaMessenger.Publish(new AddAccountForAppMessage(deviceId, appInfo));
            }
        }
    }
}
