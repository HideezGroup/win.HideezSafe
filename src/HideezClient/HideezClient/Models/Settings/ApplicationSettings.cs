using HideezMiddleware.Settings;
using System;

namespace HideezClient.Models.Settings
{
    [Serializable]
    public class ApplicationSettings : BaseSettings
    {
        /// <summary>
        /// Initializes new instance of <see cref="ApplicationSettings"/> with default values
        /// </summary>
        public ApplicationSettings()
        {
            SettingsVersion = new Version(1, 3, 1);
            IsFirstLaunch = true;
            LaunchApplicationOnStartup = false;
            SelectedUiLanguage = string.Empty;
            AddEnterAfterInput = false;
            LimitPasswordEntry = false;
            UseSimplifiedUI = false;
            AutoCreateAccountIfNotFound = false;
            AddMainDomain = true;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="copy">Intance to copy from</param>
        public ApplicationSettings(ApplicationSettings copy)
            :this()
        {
            if (copy == null)
                return;

            SettingsVersion = (Version)copy.SettingsVersion.Clone();
            IsFirstLaunch = copy.IsFirstLaunch;
            LaunchApplicationOnStartup = copy.LaunchApplicationOnStartup;
            SelectedUiLanguage = copy.SelectedUiLanguage;
            AddEnterAfterInput = copy.AddEnterAfterInput;
            LimitPasswordEntry = copy.LimitPasswordEntry;
            UseSimplifiedUI = copy.UseSimplifiedUI;
            AutoCreateAccountIfNotFound = copy.AutoCreateAccountIfNotFound;
            AddMainDomain = copy.AddMainDomain;
        }

        [Setting]
        public Version SettingsVersion { get; }

        [Setting]
        public bool IsFirstLaunch { get; set; }
        
        [Setting]
        public bool LaunchApplicationOnStartup { get; set; }

        [Setting]
        public string SelectedUiLanguage { get; set; }

        [Setting]
        public bool AddEnterAfterInput { get; set; }

        [Setting]
        public bool LimitPasswordEntry { get; set; }

        [Setting]
        public bool UseSimplifiedUI { get; set; }

        [Setting]
        public bool AutoCreateAccountIfNotFound { get; set; }

        [Setting]
        public bool AddMainDomain { get; set; }

        public override object Clone()
        {
            return new ApplicationSettings(this);
        }
    }
}
