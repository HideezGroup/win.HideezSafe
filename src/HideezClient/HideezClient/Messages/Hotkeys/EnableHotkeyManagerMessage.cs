using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages.Hotkeys
{
    /// <summary>
    /// Used to enable hotkey manager, subsequently registering all hotkeys enabled in settings.
    /// </summary>
    internal sealed class EnableHotkeyManagerMessage : PubSubMessageBase
    {
    }
}
