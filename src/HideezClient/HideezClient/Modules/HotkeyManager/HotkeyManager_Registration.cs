using HideezClient.Messages.Hotkeys;
using HideezClient.Models;
using NHotkey;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HideezClient.Modules.HotkeyManager
{
    internal sealed partial class HotkeyManager
    {
        /// <summary>
        /// Convert from string to KeyGesture
        /// </summary>
        KeyGesture ConvertStringKeysToKeyGesture(string keystroke)
        {
            return (KeyGesture)keyGestureConverter.ConvertFromString(keystroke);
        }

        async Task SubscribeAllHotkeys()
        {
            var settings = await _hotkeySettingsManager.GetSettingsAsync();
            foreach (var hotkey in settings.Hotkeys.Where(h => h.Enabled))
            {
                await SubscribeHotkey(hotkey);

                _subscribedHotkeys.Add(hotkey);
            }
        }

        async Task UnsubscribeAllHotkeys()
        {
            foreach (var hotkey in _subscribedHotkeys)
            {
                await UnsubscribeHotkey(hotkey);
            }

            _subscribedHotkeys.Clear();
        }

        async Task SubscribeHotkey(Hotkey hotkey)
        {
            try
            {
                if (hotkey.Enabled && !string.IsNullOrWhiteSpace(hotkey.Keystroke))
                {
                    EventHandler<HotkeyEventArgs> hotkeyTriggerHandler = async (e, a) =>
                    {
                        await _metaMessenger?.Publish(new HotkeyPressedMessage(hotkey.Action, hotkey.Keystroke));
                    };

                    KeyGesture kg = ConvertStringKeysToKeyGesture(hotkey.Keystroke);
                    RegisterOrUpdateHotkeyGesture(HotkeyNameFormatter.GetHotkeyName(hotkey.HotkeyId, hotkey.Action), kg.Key, kg.Modifiers, hotkeyTriggerHandler);
                    await _metaMessenger?.Publish(new HotkeyStateChangedMessage(hotkey.HotkeyId, HotkeyState.Subscribed));
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);

                await _metaMessenger?.Publish(new HotkeyStateChangedMessage(hotkey.HotkeyId, HotkeyState.Unavailable));
            }
        }

        async Task UnsubscribeHotkey(Hotkey hotkey)
        {
            try
            {
                RemoveHotkeyGesture(HotkeyNameFormatter.GetHotkeyName(hotkey.HotkeyId, hotkey.Action));
                await _metaMessenger?.Publish(new HotkeyStateChangedMessage(hotkey.HotkeyId, HotkeyState.Unsubscribed));
            }
            catch (Exception ex)
            {
                try
                {
                    WriteLine(ex);

                    await _metaMessenger?.Publish(new HotkeyStateChangedMessage(hotkey.HotkeyId, HotkeyState.Unavailable));
                }
                catch (Exception exc)
                {
                    WriteLine(exc);
                }
            }
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
