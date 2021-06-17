using HideezClient.Models.Settings;
using HideezClient.Mvvm;
using HideezMiddleware.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezClient.ViewModels
{
    class MaximizeWindowSettingViewModel: LocalizedObject
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

                    if (_isChecked != _appSettingsManager.Settings.MaximizeWindowsOnOpening)
                        TrySaveChanges(value);

                    NotifyPropertyChanged();
                }
            }
        }

        public MaximizeWindowSettingViewModel(ISettingsManager<ApplicationSettings> appSettingsManager)
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
            IsChecked = _appSettingsManager.Settings.MaximizeWindowsOnOpening;
        }

        void TrySaveChanges(bool newValue)
        {
            try
            {
                var settings = _appSettingsManager.Settings;
                settings.MaximizeWindowsOnOpening = newValue;
                _appSettingsManager.SaveSettings(settings);
            }
            catch (Exception) { }
        }
    }
}
