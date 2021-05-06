using HideezClient.Messages.Hotkeys;
using HideezClient.Models;
using HideezClient.Models.Settings;
using HideezClient.Modules;
using HideezMiddleware.Settings;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Commands;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HideezClient.ViewModels
{
    class HotkeySettingsViewModel : ReactiveObject
    {
        private readonly ISettingsManager<HotkeySettings> _settingsManager;
        private readonly IWindowsManager _windowsManager;
        private readonly IMetaPubSub _metaMessenger;

        public HotkeySettingsViewModel(ISettingsManager<HotkeySettings> settingsManager, IWindowsManager windowsManager, IMetaPubSub metaMessenger)
        {
            _settingsManager = settingsManager;
            _windowsManager = windowsManager;
            _metaMessenger = metaMessenger;

            settingsManager.SettingsChanged += SettingsManager_SettingsChanged;

            UpdateHotkeys(settingsManager.Settings.Hotkeys);
        }

        public ObservableCollection<HotkeyViewModel> Hotkeys { get; private set; } = new ObservableCollection<HotkeyViewModel>();

        #region Commands
        public ICommand AddHotkeyCommand
        {
            get => new DelegateCommand
            {
                CommandAction = x =>
                {
                    Task.Run(OnAddHotkey);
                }
            };
        }

        public ICommand ResetToDefaultCommand
        {
            get => new DelegateCommand
            {
                CommandAction = x =>
                {
                    Task.Run(ResetToDefault);
                }
            };
        }
        #endregion

        async Task OnAddHotkey()
        {
            if (Hotkeys.Count < 20)
                await _metaMessenger.Publish(new AddHotkeyMessage(string.Empty, UserAction.None));
        }

        async Task ResetToDefault()
        {
            var result = await _windowsManager.ShowResetToDefaultHotkeysAsync();
            if (result)
                _settingsManager.SaveSettings(new HotkeySettings());
        }

        void SettingsManager_SettingsChanged(object sender, SettingsChangedEventArgs<HotkeySettings> e)
        {
            UpdateHotkeys(e.NewSettings.Hotkeys);
        }

        void UpdateHotkeys(Hotkey[] hotkeys)
        {
            if (hotkeys.Length < Hotkeys.Count)
                RemoveNeedlessViewModels(hotkeys);

            foreach (var hotkey in hotkeys)
            {
                var existHotkey = Hotkeys.FirstOrDefault(h => h.HotkeyId == hotkey.HotkeyId);
                if (existHotkey == null)
                {
                    var newViewModel = new HotkeyViewModel(hotkey, _metaMessenger);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Hotkeys.Add(newViewModel);
                    });
                }
                else
                {
                    existHotkey.UpdateViewModel(hotkey);
                }
            }
        }

        void RemoveNeedlessViewModels(Hotkey[] hotkeys)
        {
            var needlessHotkeys = Hotkeys.Where(h => hotkeys.FirstOrDefault(hotkeyModel => hotkeyModel.HotkeyId == h.HotkeyId) == null).ToArray();
            foreach (var hotkey in needlessHotkeys)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Hotkeys.Remove(hotkey);
                });
            }
        }

        public async Task ChangeHotkeyManagerState(bool isEnabled)
        {
            if (isEnabled)
                await _metaMessenger.Publish(new EnableHotkeyManagerMessage());
            else
                await _metaMessenger.Publish(new DisableHotkeyManagerMessage());
        }

        public async Task ReloadHotkeys()
        {
            bool hasError = Hotkeys.FirstOrDefault(h => string.IsNullOrEmpty(h.ErrorKeystroke)) == null;
            if(hasError)
                await _metaMessenger.Publish(new ResetHotkeysMessage());
        }
    }
}
