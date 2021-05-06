using HideezClient.Models;
using System;

namespace HideezClient.Modules.HotkeyManager
{
    internal sealed class HotkeyNameFormatter
    {
        // Unique prefix for our app is required to avoid collision 
        // with hotkeys that may be registered by other applications
        const string GLOBAL_PREFIX = "HideezClient_Hotkey";

        public static string GetHotkeyName(int hotkeyId, UserAction action)
        {
            var actionName = Enum.GetName(typeof(UserAction), action);

            return $"{GLOBAL_PREFIX}_{hotkeyId}_{actionName}"; //e.g. HideezClient_Hotkey_1_InputLogin
        }
    }
}
