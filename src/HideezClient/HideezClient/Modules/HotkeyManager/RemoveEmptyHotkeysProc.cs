using HideezClient.Models;
using HideezClient.Models.Settings;
using HideezMiddleware.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HideezClient.Modules.HotkeyManager
{
    class RemoveEmptyHotkeysProc
    {
        readonly ISettingsManager<HotkeySettings> _hotkeySettingsManager;

        public RemoveEmptyHotkeysProc(ISettingsManager<HotkeySettings> hotkeySettingsManager)
        {
            _hotkeySettingsManager = hotkeySettingsManager;
        }

        public async Task Run()
        {
            HotkeySettings settings = await _hotkeySettingsManager.GetSettingsAsync();
            var emptySettings = settings.Hotkeys.Where(h => h.Action == UserAction.None || string.IsNullOrEmpty(h.Keystroke)).ToArray();
            foreach (var hotkey in emptySettings)
            {
                settings.RemoveHotkey(hotkey.HotkeyId);
            }

            _hotkeySettingsManager.SaveSettings(settings);
        }
    }
}
