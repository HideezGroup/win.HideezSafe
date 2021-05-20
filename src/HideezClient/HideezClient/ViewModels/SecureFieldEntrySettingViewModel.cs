using HideezClient.Models.Settings;
using HideezClient.Mvvm;
using HideezMiddleware.Settings;
using System;

using System.Threading.Tasks;

namespace HideezClient.ViewModels
{
    class SecureFieldEntrySettingViewModel : LocalizedObject
    {
        readonly ISettingsManager<ApplicationSettings> _appSettingsManager;
        bool _isChecked;

        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    NotifyPropertyChanged();
                    Task.Run(() => SaveChanges(_isChecked)).ConfigureAwait(false);
                }
            }
        }

        public SecureFieldEntrySettingViewModel(ISettingsManager<ApplicationSettings> appSettingsManager)
        {
            _appSettingsManager = appSettingsManager;

            _appSettingsManager.SettingsChanged += AppSettingsManager_SettingsChanged;
            LoadSetting();
        }

        private void AppSettingsManager_SettingsChanged(object sender, SettingsChangedEventArgs<ApplicationSettings> e)
        {
            LoadSetting();
        }

        void LoadSetting()
        {
            IsChecked = _appSettingsManager.Settings.LimitPasswordEntry;
        }

        void SaveChanges(bool newValue)
        {
            try
            {
                var settings = _appSettingsManager.Settings;
                settings.LimitPasswordEntry = newValue;
                _appSettingsManager.SaveSettings(settings);
            }
            catch (Exception) { }
        }
    }
}
