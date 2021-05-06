using HideezClient.Modules.HotkeyManager;
using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages.Hotkeys
{
    internal sealed class HotkeyStateChangedMessage : PubSubMessageBase
    {
        public int HotkeyId { get; set; }

        public HotkeyState State { get; set; }

        public HotkeyStateChangedMessage(int hotkeyId, HotkeyState state)
        {
            HotkeyId = hotkeyId;
            State = state;
        }
    }
}
