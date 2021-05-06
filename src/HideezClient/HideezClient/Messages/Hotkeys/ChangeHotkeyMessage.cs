using HideezClient.Models;
using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages.Hotkeys
{
    internal sealed class ChangeHotkeyMessage : PubSubMessageBase
    {
        public int HotkeyId { get; set; }

        public bool Enabled { get; set; }

        public UserAction Action { get; set; }

        public string Keystroke { get; set; }

        public ChangeHotkeyMessage(int hotkeyId, bool enabled, UserAction action, string keystroke)
        {
            HotkeyId = hotkeyId;
            Enabled = enabled;
            Action = action;
            Keystroke = keystroke;
        }
    }
}
