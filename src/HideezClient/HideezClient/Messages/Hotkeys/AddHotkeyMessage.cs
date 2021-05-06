using HideezClient.Models;
using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages.Hotkeys
{
    /// <summary>
    /// Request new hotkey entry to be added to the HotkeySettings
    /// </summary>
    internal sealed class AddHotkeyMessage : PubSubMessageBase
    {
        public UserAction Action { get; set; }

        public string Keystroke { get; set; }

        public AddHotkeyMessage(string keystroke, UserAction action)
        {
            Keystroke = keystroke;
            Action = action;
        }
    }
}
