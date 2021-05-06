using HideezMiddleware.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HideezClient.Models.Settings
{
    [Serializable]
    public class HotkeySettings : BaseSettings
    {
        /// <summary>
        /// Initializes new instance of <see cref="HotkeySettings"/> with default values
        /// </summary>
        public HotkeySettings()
        {
            SettingsVersion = new Version(1, 2, 0);
            Hotkeys = new Hotkey[]
            {
                new Hotkey(1, true, UserAction.InputLogin, "Control + Alt + L"),
                new Hotkey(2, true, UserAction.InputPassword, "Control + Alt + P"),
                new Hotkey(3, true, UserAction.InputOtp, "Control + Alt + O"),
                new Hotkey(4, true, UserAction.AddAccount, "Control + Alt + A"),
            };
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="copy">Intance to copy from</param>
        public HotkeySettings(HotkeySettings copy)
            :base()
        {
            if (copy == null)
                return;

            SettingsVersion = (Version)copy.SettingsVersion.Clone();
            Hotkeys = copy.Hotkeys.Select(h => h.DeepCopy()).ToArray();
        }


        [Setting]
        public Version SettingsVersion { get; }

        [Setting]
        public Hotkey[] Hotkeys { get; set; }

        public void AddHotkey(Hotkey hotkey)
        {
            Hotkeys = Hotkeys.Append(hotkey).ToArray();
        }

        public int RemoveHotkey(int id)
        {
            int startCount = Hotkeys.Length;
            Hotkeys = Hotkeys.Where(h => h.HotkeyId != id).ToArray();
            return startCount - Hotkeys.Length;
        }

        public override object Clone()
        {
            return new HotkeySettings(this);
        }
    }
}
