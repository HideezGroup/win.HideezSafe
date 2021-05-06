using System;
using HideezClient.Models.Settings;
using HideezClient.Models;
using NHotkey;
using GalaSoft.MvvmLight.Messaging;
using HideezClient.Messages;
using System.Windows;
using System.Threading.Tasks;
using HideezClient.Utilities;
using System.IO;
using System.Windows.Input;
using HideezMiddleware.Settings;
using Hideez.SDK.Communication.Log;
using HideezClient.Modules.Log;
using Meta.Lib.Modules.PubSub;
using HideezClient.Messages.Hotkeys;
using System.Linq;
using HideezMiddleware.Threading;
using System.Collections.Generic;

namespace HideezClient.Modules.HotkeyManager
{
    /// <summary>
    /// Responsible for managing hotkeys registration, notification about their status and hotkey trigger notification
    /// <para>
    /// Subscribed messages
    /// <list type="bullet">
    /// <item><see cref="SettingsChangedMessage<HotkeySettings>"/></item>
    /// <item><see cref="ResetHotkeysMessage"/></item>
    /// <item><see cref="EnableHotkeyManagerMessage"/></item>
    /// <item><see cref="DisableHotkeyManagerMessage"/></item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed partial class HotkeyManager : Logger, IHotkeyManager
	{
		readonly KeyGestureConverter keyGestureConverter = new KeyGestureConverter();
		readonly ISettingsManager<HotkeySettings> _hotkeySettingsManager;
        readonly IMetaPubSub _metaMessenger;
        bool enabled = false;

        readonly SemaphoreQueue registrationQueue = new SemaphoreQueue(1, 1);
        List<Hotkey> _subscribedHotkeys = new List<Hotkey>();

        public HotkeyManager(ISettingsManager<HotkeySettings> hotkeySettingsManager, IMetaPubSub metaMessenger, ILog log)
            : base(nameof(HotkeyManager), log)
		{
			_hotkeySettingsManager = hotkeySettingsManager;
            hotkeySettingsManager.SettingsFilePath = Path.Combine(Constants.DefaultSettingsFolderPath, "hotkeys.xml"); ;
            _metaMessenger = metaMessenger;

            _metaMessenger.Subscribe<SettingsChangedMessage<HotkeySettings>>(OnHotkeysSettingsChanged);

            _metaMessenger.Subscribe<ResetHotkeysMessage>(OnResetHotkeys);
            _metaMessenger.Subscribe<EnableHotkeyManagerMessage>(OnEnableHotkeyManager);
            _metaMessenger.Subscribe<DisableHotkeyManagerMessage>(OnDisableHotkeyManager);
        }

        /// <summary>
        /// If set to true, the available hotkeys will be registered by manager. 
        /// If set to false, the hotkeys will be unregistered and may be claimed by other applications.
        /// </summary>
        bool Enabled
        {
            get
            {
                return enabled;
            }
            set
            {
                if (enabled != value)
                {
                    enabled = value;
                    if (Enabled)
                        Task.Run(ResetHotkeys);
                    else
                        Task.Run(DisableHotkeys);
                }
            }
        }

        #region Message handlers
        Task OnHotkeysSettingsChanged(SettingsChangedMessage<HotkeySettings> message)
        {
            if (Enabled)
                Task.Run(ResetHotkeys);

            return Task.CompletedTask;
        }

        Task OnResetHotkeys(ResetHotkeysMessage msg)
        {
            Task.Run(ResetHotkeys);

            return Task.CompletedTask;
        }

        Task OnEnableHotkeyManager(EnableHotkeyManagerMessage msg)
        {
            Enabled = true;

            return Task.CompletedTask;
        }

        Task OnDisableHotkeyManager(DisableHotkeyManagerMessage msg)
        {
            Enabled = false;

            return Task.CompletedTask;
        }
        #endregion

        async Task ResetHotkeys()
        {
            try
            {
                await registrationQueue.WaitAsync();

                await UnsubscribeAllHotkeys();
                await SubscribeAllHotkeys();
            }
            finally
            {
                registrationQueue.Release();
            }
        }

        async Task EnableHotkeys()
        {
            try
            {
                await registrationQueue.WaitAsync();
                await SubscribeAllHotkeys();
            }
            finally
            {
                registrationQueue.Release();
            }
        }

        async Task DisableHotkeys()
        {
            try
            {
                await registrationQueue.WaitAsync();
                await UnsubscribeAllHotkeys();
            }
            finally
            {
                registrationQueue.Release();
            }
        }

        /// <summary>
        /// Returns hotkey combination registered for specified action
        /// </summary>
        /// <param name="action">Hotkey action</param>
        /// <returns>Key combination if hotkey is registered to action in settings. Otherwise returns empty string.</returns>
		public async Task<string> GetEnabledKeystrokeForAction(UserAction action)
		{
            var settings = await _hotkeySettingsManager.GetSettingsAsync();
            var hotkey = settings.Hotkeys.Where(h => h.Enabled && h.Action == action).
                Select(h => h.Keystroke).FirstOrDefault();
			return hotkey ?? string.Empty;
		}

        /// <summary>
        /// Check if the specified hotkey is already used for another action in our application
        /// </summary>
        /// <param name="action">Action registered for hotkey</param>
        /// <param name="keystroke">String representation of keys combination</param>
        /// <returns>Returns true if hotkey is used only once in current application. Otherwise returns false.</returns>
        public async Task<bool> IsUniqueKeystroke(int hotkeyId, string keystroke)
        {
            var settings = await _hotkeySettingsManager.GetSettingsAsync();
            foreach (var h in settings.Hotkeys)
            {
                if (h.HotkeyId != hotkeyId && h.Keystroke == keystroke)
                    return false;
            }

            return true;
        }
    }
}
