using Hideez.SDK.Communication.Log;
using HideezClient.Messages;
using HideezClient.Models.Settings;
using HideezMiddleware.Localize;
using HideezMiddleware.Settings;
using Meta.Lib.Modules.PubSub;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace HideezClient.Modules.Subroutines
{
    /// <summary>
    /// Wait application settings loading and then define installed culture
    /// </summary>
    internal sealed class DefineInstalledCultureSubroutine: SubroutineBase
    {
        public DefineInstalledCultureSubroutine(IMetaPubSub messenger, ILog log)
            : base(messenger, nameof(ShowMainWindowAfterStartupSubroutine), log)
        {
            _messenger.Subscribe<ApplicationSettingsLoadedMessage>(OnApplicationSettingsLoaded);
            _ = SafePublish(new SubroutineCreated(this));
        }

        private async Task OnApplicationSettingsLoaded(ApplicationSettingsLoadedMessage arg)
        {
            await _messenger.Unsubscribe<ApplicationSettingsLoadedMessage>(OnApplicationSettingsLoaded);

            var settings = arg.ApplicationSettings;
            var currentCulture = CultureInfo.InstalledUICulture;
            CultureInfo culture = null;

            if (currentCulture.Name == "ru-RU" || currentCulture.Name == "uk-UA")
                culture = new CultureInfo("uk-UA");
            else
                culture = new CultureInfo("en-US");

            settings.SelectedUiLanguage = culture.Name;

            await SafePublish(new SubroutineFinished(this));
        }
    }
}
