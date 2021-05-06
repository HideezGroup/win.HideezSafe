using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages.Hotkeys
{
    internal sealed class DeleteHotkeyMessage : PubSubMessageBase
    {
        public int HotkeyId { get; set; }

        public DeleteHotkeyMessage(int hotkeyId)
        {
            HotkeyId = hotkeyId;
        }
    }
}
