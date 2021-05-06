using HideezClient.Modules.HotkeyManager;
using Meta.Lib.Modules.PubSub;
using System.Collections.Generic;

namespace HideezClient.Messages.Hotkeys
{
    internal sealed class GetHotkeyStatesMessageReply : PubSubMessageBase
    {
        public Dictionary<int, HotkeyState> HotkeyStates = new Dictionary<int, HotkeyState>();

        public GetHotkeyStatesMessageReply(Dictionary<int, HotkeyState> hotkeyStates)
        {
            HotkeyStates = hotkeyStates;
        }
    }
}
