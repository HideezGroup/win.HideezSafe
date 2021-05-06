using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages.Hotkeys
{
    /// <summary>
    /// Used to turn off the hotkey manager, subsequently unregistering all registered hotkeys
    /// </summary>
    internal sealed class DisableHotkeyManagerMessage : PubSubMessageBase
    {
    }
}
