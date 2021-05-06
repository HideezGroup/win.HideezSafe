using HideezClient.Messages.Hotkeys;
using HideezClient.Models;
using HideezClient.Models.Settings;
using HideezMiddleware.Settings;
using Meta.Lib.Modules.PubSub;
using System.Linq;
using System.Threading.Tasks;

namespace HideezClient.Modules.HotkeyManager
{
    /// <summary>
    /// Responsible for handling messages about creating, updating and removing hotkey settings. 
    /// Exceptions should be handled by providers of messages.
    /// <para>
    /// Subscribed messages:
    /// <list type="bullet">
    /// <item><see cref="AddHotkeyMessage"/></item>
    /// <item><see cref="ChangeHotkeyMessage"/></item>
    /// <item><see cref="DeleteHotkeyMessage"/></item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class HotkeySettingsController : IHotkeySettingsController
    {
        readonly ISettingsManager<HotkeySettings> _hotkeySettingsManager;
        readonly IMetaPubSub _metaMessenger;

        public HotkeySettingsController(ISettingsManager<HotkeySettings> hotkeySettingsManager, IMetaPubSub metaMessenger)
        {
            _hotkeySettingsManager = hotkeySettingsManager;
            _metaMessenger = metaMessenger;

            _metaMessenger.Subscribe<AddHotkeyMessage>(OnAddHotkey);
            _metaMessenger.Subscribe<ChangeHotkeyMessage>(OnChangeHotkey);
            _metaMessenger.Subscribe<DeleteHotkeyMessage>(OnDeleteHotkey);
        }

        private Task OnAddHotkey(AddHotkeyMessage msg)
        {
            HotkeySettings settings = (HotkeySettings)_hotkeySettingsManager.Settings.Clone();

            // Theoretically may overflow, but can be fixed by erasing all hotkeys
            // and, realistically, will never happen during normal use
            int id = 0;
            if (settings.Hotkeys.Length > 0)
                id = settings.Hotkeys.Select(h => h.HotkeyId).Max() + 1;

            var newHotkey = new Hotkey
            {
                HotkeyId = id,
                Enabled = true,
                Action = msg.Action,
                Keystroke = msg.Keystroke,
            };
            settings.AddHotkey(newHotkey);
            _hotkeySettingsManager.SaveSettings(settings);

            return Task.CompletedTask;
        }

        private Task OnChangeHotkey(ChangeHotkeyMessage msg)
        {
            HotkeySettings settings = (HotkeySettings)_hotkeySettingsManager.Settings.Clone();
            var hotkey = settings.Hotkeys.FirstOrDefault(h => h.HotkeyId == msg.HotkeyId);

            if (hotkey != null)
            {
                hotkey.Enabled = msg.Enabled;
                hotkey.Action = msg.Action;
                hotkey.Keystroke = msg.Keystroke;

                _hotkeySettingsManager.SaveSettings(settings);
            }

            return Task.CompletedTask;
        }

        private Task OnDeleteHotkey(DeleteHotkeyMessage msg)
        {
            HotkeySettings settings = (HotkeySettings)_hotkeySettingsManager.Settings.Clone();
            var removedCount = settings.RemoveHotkey(msg.HotkeyId);
            if (removedCount > 0)
                _hotkeySettingsManager.SaveSettings(settings);

            return Task.CompletedTask;
        }
    }
}
