using HideezClient.Models.Settings;
using HideezClient.Modules;
using HideezClient.Mvvm;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.Localize;
using HideezMiddleware.Settings;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HideezClient.ViewModels
{
    class LanguageSettingViewModel : LocalizedObject
    {
        public class LanguageCulture
        {
            public string Name { get; set; }

            public string Title { get => TranslationSource.Instance[Name]; }
        }

        readonly ISettingsManager<ApplicationSettings> _appSettingsManager;
        readonly IWindowsManager _windowsManager;
        readonly IMetaPubSub _messenger;
        readonly string _currentLanguageName;

        LanguageCulture _selectedLanguage;
        bool _hasChanges;

        public LanguageSettingViewModel(
            ISettingsManager<ApplicationSettings> appSettingsManager, 
            IWindowsManager windowsManager,
            IMetaPubSub messenger)
        {
            _appSettingsManager = appSettingsManager;
            _windowsManager = windowsManager;
            _messenger = messenger;

            _appSettingsManager.SettingsChanged += AppSettingsManager_SettingsChanged;
            _currentLanguageName = _appSettingsManager.Settings.SelectedUiLanguage;
            LoadSetting();
        }

        public List<LanguageCulture> Languages { get; } = new List<LanguageCulture>()
        {
            new LanguageCulture {Name = "en-US"},
            new LanguageCulture {Name = "uk-UA"}
        };

        public LanguageCulture SelectedLanguage 
        {
            get => _selectedLanguage;
            set
            {
                if(_selectedLanguage != value)
                {
                    _selectedLanguage = value;

                    if (_selectedLanguage.Name != _currentLanguageName)
                    {
                        TrySaveChanges(value.Name);
                        HasChanges = true;
                    }
                    else
                        HasChanges = false;

                    NotifyPropertyChanged();
                }
            }
        }

        public bool HasChanges 
        { 
            get => _hasChanges;
            set 
            { 
                if(_hasChanges != value)
                {
                    _hasChanges = value; 
                    NotifyPropertyChanged();
                }
            }
        }

        public ICommand RestartCommand
        {
            get => new DelegateCommand
            {
                CommandAction = x =>
                {
                    Task.Run(OnRestartApp);
                }
            };
        }

        private void OnRestartApp()
        {
            try
            {
                _messenger.PublishOnServer(new LanguageSettingsChangedMessage(SelectedLanguage.Name));
            }
            catch { }

            _windowsManager.RestartApplication();
        }

        private void AppSettingsManager_SettingsChanged(object sender, SettingsChangedEventArgs<ApplicationSettings> e)
        {
            LoadSetting();
        }

        void LoadSetting()
        {
            SelectedLanguage = Languages.FirstOrDefault(l => l.Name == _appSettingsManager.Settings.SelectedUiLanguage);
        }

        void TrySaveChanges(string newValue)
        {
            try
            {
                var settings = _appSettingsManager.Settings;
                settings.SelectedUiLanguage = newValue;
                _appSettingsManager.SaveSettings(settings);
            }
            catch (Exception) { }
        }
    }
}
