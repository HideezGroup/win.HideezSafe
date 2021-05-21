using HideezClient.Models.Settings;
using HideezClient.Mvvm;
using HideezMiddleware.Settings;
using System;
using System.Diagnostics;
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
                    var oldValue = _isChecked;
                    _isChecked = value;

                    if (_isChecked != _appSettingsManager.Settings.LimitPasswordEntry)
                        TrySaveChanges(oldValue, value);

                    NotifyPropertyChanged();
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

        void TrySaveChanges(bool oldValue, bool newValue)
        {
            bool uacSuccess = false;
            try
            {
                var proc = new Process();
                proc.StartInfo.FileName = System.Reflection.Assembly.GetExecutingAssembly().Location;
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Verb = "runas";
                proc.StartInfo.Arguments = App.ImmediateShutdownParam;
                uacSuccess = proc.Start();
            }
            catch (Exception) { }

            if (uacSuccess)
            {
                try
                {
                    var settings = _appSettingsManager.Settings;
                    settings.LimitPasswordEntry = newValue;
                    _appSettingsManager.SaveSettings(settings);
                }
                catch (Exception) { }
            }
            else
            {
                IsChecked = oldValue;
            }
        }
    }
}
