using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.IPC.IncommingMessages
{
    public class LanguageSettingsChangedMessage: PubSubMessageBase
    {
        public string NewLanguageName { get; }

        public LanguageSettingsChangedMessage(string newLanguageName)
        {
            NewLanguageName = newLanguageName;
        }
    }
}
