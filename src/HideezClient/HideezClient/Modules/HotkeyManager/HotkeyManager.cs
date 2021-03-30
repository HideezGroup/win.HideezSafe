﻿using System;
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

namespace HideezClient.Modules.HotkeyManager
{
    /// <summary>
    /// Responsible for managing hotkeys registration, notification about their status and hotkey input notification
    /// </summary>
    internal sealed class HotkeyManager : IHotkeyManager
	{
        readonly Logger _log = LogManager.GetCurrentClassLogger(nameof(HotkeyManager));
		readonly KeyGestureConverter keyGestureConverter = new KeyGestureConverter();
		readonly ISettingsManager<HotkeySettings> hotkeySettingsManager;
        readonly IMetaPubSub _metaMessenger;
        bool enabled = false;

		public HotkeyManager(ISettingsManager<HotkeySettings> hotkeySettingsManager, IMetaPubSub metaMessenger)
		{
			this.hotkeySettingsManager = hotkeySettingsManager;
            hotkeySettingsManager.SettingsFilePath = Path.Combine(Constants.DefaultSettingsFolderPath, "hotkeys.xml");
            _metaMessenger = metaMessenger;
            _metaMessenger.Subscribe<SettingsChangedMessage<HotkeySettings>>(OnHotkeysSettingsChanged);
		}

        /// <summary>
        /// If set to true, the available hotkeys will be registered by manager. 
        /// If set to false, the hotkeys will be unregistered and may be claimed by other applications.
        /// </summary>
        public bool Enabled
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
                    OnManagerEnabledChanged(value);
                }
            }
        }

        async Task OnHotkeysSettingsChanged(SettingsChangedMessage<HotkeySettings> message)
        {
            if (Enabled)
            {
                await Task.Run(async () =>
                {
                    await UnsubscribeAllHotkeys();
                    await SubscribeAllHotkeys();
                });
            }
        }

        void OnManagerEnabledChanged(bool newValue)
        {
            Task.Run(async () =>
            {
                if (newValue)
                {
                    await UnsubscribeAllHotkeys();
                    await SubscribeAllHotkeys();
                }
                else
                {
                    await UnsubscribeAllHotkeys();
                }
            });
        }

        /// <summary>
        /// Returns hotkey combination registered for specified action
        /// </summary>
        /// <param name="action">Hotkey action</param>
        /// <returns>Key combination if hotkey is registered to action in settings. Otherwise returns empty string.</returns>
		public async Task<string> GetHotkeyForAction(UserAction action)
		{
            var settings = await hotkeySettingsManager.GetSettingsAsync();
            settings.Hotkeys.TryGetValue(action, out string hotkey);
			return hotkey ?? string.Empty;
		}

        /// <summary>
        /// Check if the specified key combination is taken by other application. 
        /// </summary>
        /// <param name="action">Action registered for hotkey</param>
        /// <param name="hotkey">String representation of keys combination</param>
        /// <returns>Returns true if the specified hotkey can be registered. Otherwise returns false.</returns>
        public async Task<bool> IsFreeHotkey(UserAction action, string hotkey)
		{
            if (string.IsNullOrEmpty(hotkey))
                return true;

            try
            {
                if (Enabled)
                    await UnsubscribeAllHotkeys();

                try
                {
                    var testHotkeyName = "_hideez_test_" + Guid.NewGuid().ToString();

                    System.Windows.Input.KeyGesture kg = ConvertStringKeysToKeyGesture(hotkey);
                    RegisterOrUpdateHotkeyGesture(testHotkeyName, kg.Key, kg.Modifiers, (o, e) => { });
                    RemoveHotkeyGesture(testHotkeyName);
                    return true;
                }
                catch (Exception) // If an exception occures, it means that hotkey is already taken
                {
                    return false;
                }
            }
            finally
            {
                if (Enabled)
                    await SubscribeAllHotkeys();
            }
		}

        /// <summary>
        /// Check if the specified hotkey is already used for another action in our application
        /// </summary>
        /// <param name="action">Action registered for hotkey</param>
        /// <param name="hotkey">String representation of keys combination</param>
        /// <returns>Returns true if hotkey is used only once in current application. Otherwise returns false.</returns>
        public async Task<bool> IsUniqueHotkey(UserAction action, string hotkey)
        {
            var settings = await hotkeySettingsManager.GetSettingsAsync();
            foreach (var h in settings.Hotkeys)
            {
                if (h.Key != action && h.Value == hotkey)
                    return false;
            }

            return true;
        }

		async Task SubscribeAllHotkeys()
		{
            var settings = await hotkeySettingsManager.GetSettingsAsync();
            foreach (var shortcut in settings.Hotkeys)
			{
				SubscribeHotkey(shortcut.Key, shortcut.Value);
			}
		}

		async Task UnsubscribeAllHotkeys()
		{
            var settings = await hotkeySettingsManager.GetSettingsAsync();
            foreach (var item in settings.Hotkeys)
			{
				await UnsubscribeHotkey(item.Key);
			}
		}

		void SubscribeHotkey(UserAction action, string hotkey)
		{
			try
			{
				if (!string.IsNullOrEmpty(hotkey))
				{
					KeyGesture kg = ConvertStringKeysToKeyGesture(hotkey);
                    RegisterOrUpdateHotkeyGesture(Enum.GetName(typeof(UserAction), action), kg.Key, kg.Modifiers, OnHotkeyInput);

                    _metaMessenger?.Publish(new HotkeyStateChangedMessage(action, hotkey, HotkeyState.Subscribed));
                }
            }
			catch (Exception ex)
			{
                _log.WriteLine(ex);

                _metaMessenger?.Publish(new HotkeyStateChangedMessage(action, hotkey, HotkeyState.Unavailable));
			}
		}

		async Task UnsubscribeHotkey(UserAction action)
		{
			try
			{
                RemoveHotkeyGesture(Enum.GetName(typeof(UserAction), action));
                _metaMessenger?.Publish(new HotkeyStateChangedMessage(action, await GetHotkeyForAction(action), HotkeyState.Unsubscribed));
            }
            catch (Exception ex)
			{
                try
                {
                    _log.WriteLine(ex);

                    _metaMessenger?.Publish(new HotkeyStateChangedMessage(action, await GetHotkeyForAction(action), HotkeyState.Unavailable));
                }
                catch (Exception exc)
                {
                    _log.WriteLine(exc);
                }
            }
        }

		/// <summary>
		/// Convert from string to KeyGesture
		/// </summary>
		/// <param name="keys">
		/// Hot key 
		/// </param>
		KeyGesture ConvertStringKeysToKeyGesture(string keys)
		{
			return (KeyGesture)keyGestureConverter.ConvertFromString(keys);
		}

		void OnHotkeyInput(object sender, HotkeyEventArgs e)
		{
            Task.Run(async () =>
            {
                var settings = await hotkeySettingsManager.GetSettingsAsync();
                if (Enum.TryParse(e.Name, out UserAction action) 
                    && settings.Hotkeys.TryGetValue(action, out string hotkey))
			    {
                    _metaMessenger?.Publish(new HotkeyPressedMessage(action, hotkey));
			    }
            });
        }

        void RegisterOrUpdateHotkeyGesture(string name, Key key, ModifierKeys modifiers, EventHandler<HotkeyEventArgs> handler)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Must be executed on STA thread
                NHotkey.Wpf.HotkeyManager.Current.AddOrReplace(name, key, modifiers, handler);
            });
        }

        void RemoveHotkeyGesture(string name)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Must be executed on STA thread
                NHotkey.Wpf.HotkeyManager.Current.Remove(name);
            });
        }
    }
}
