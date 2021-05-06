using System;
namespace HideezClient.Models
{
    [Serializable]
    public class Hotkey 
    {
        public int HotkeyId { get; set; }

        public bool Enabled { get; set; }

        public UserAction Action { get; set; }

        public string Keystroke { get; set; }

        public Hotkey()
        {
            HotkeyId = 0;
            Enabled = false;
            Action = UserAction.None;
            Keystroke = string.Empty;
        }

        public Hotkey(int hotkeyId, bool enabled, UserAction action, string keystroke)
        {
            HotkeyId = hotkeyId;
            Enabled = enabled;
            Action = action;
            Keystroke = keystroke;
        }

        public Hotkey DeepCopy()
        {
            return new Hotkey(HotkeyId, Enabled, Action, string.Copy(Keystroke));
        }
    }
}
