﻿using System;

namespace HideezSafe.Models.Settings
{
    [Serializable]
    public class ApplicationSettings : BaseSettings
    {
        /// <summary>
        /// Initializes new instance of <see cref="ApplicationSettings"/> with default values
        /// </summary>
        public ApplicationSettings()
        {
            SettingsVersion = new Version(1, 0, 0);
            IsFirstLaunch = true;
            LaunchApplicationOnStartup = false;
            SelectedUiLanguage = "en-us";
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
        }

        [Setting]
        public Version SettingsVersion { get; }

        [Setting]
        public bool IsFirstLaunch { get; set; }
        
        [Setting]
        public bool LaunchApplicationOnStartup { get; set; }

        [Setting]
        public string SelectedUiLanguage { get; set; }

        public override object Clone()
        {
            return new ApplicationSettings(this);
        }
    }
}
