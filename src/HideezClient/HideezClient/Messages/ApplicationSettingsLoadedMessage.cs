using HideezClient.Models.Settings;
using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages
{
    internal sealed class ApplicationSettingsLoadedMessage: PubSubMessageBase
    {
        public ApplicationSettings ApplicationSettings { get; }

        public ApplicationSettingsLoadedMessage(ApplicationSettings applicationSettings)
        {
            ApplicationSettings = applicationSettings;
        }
    }
}
