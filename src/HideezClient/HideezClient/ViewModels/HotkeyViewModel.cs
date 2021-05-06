using HideezClient.Messages.Hotkeys;
using HideezClient.Models;
using HideezClient.Modules.HotkeyManager;
using HideezMiddleware.Localize;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Commands;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HideezClient.ViewModels
{
    class HotkeyViewModel : ReactiveObject
    {
        public class HotkeyActionOption
        {
            public string Title { get; set; }

            public UserAction Action { get; set; }
        }

        private readonly IMetaPubSub _metaMessenger;

        private string _keystroke;

        public HotkeyViewModel(Hotkey hotkeyModel, IMetaPubSub metaMessenger)
        {
            _metaMessenger = metaMessenger;

            HotkeyActionOptions = new List<HotkeyActionOption>
            {
                new HotkeyActionOption { Action = UserAction.None, Title = TranslationSource.Instance["HotkeysSettings.Actions.None"]},
                new HotkeyActionOption { Action = UserAction.AddAccount, Title = TranslationSource.Instance["HotkeysSettings.Actions.AddAccount"]},
                new HotkeyActionOption { Action = UserAction.InputLogin, Title = TranslationSource.Instance["HotkeysSettings.Actions.InputLogin"]},
                new HotkeyActionOption { Action = UserAction.InputOtp, Title = TranslationSource.Instance["HotkeysSettings.Actions.InputOtp"]},
                new HotkeyActionOption { Action = UserAction.InputPassword, Title = TranslationSource.Instance["HotkeysSettings.Actions.InputPassword"]},
            };

            HotkeyId = hotkeyModel.HotkeyId;
            UpdateViewModel(hotkeyModel);

            this.ObservableForProperty(vm => vm.IsEnabled).Subscribe(vm => { OnValueChanged(); });
            this.ObservableForProperty(vm => vm.SelectedActionOption).Subscribe(vm => { OnValueChanged(); });
            this.ObservableForProperty(vm => vm.Keystroke).Subscribe(vm => { OnValueChanged(); });

            _metaMessenger.Subscribe<HotkeyStateChangedMessage>(OnHotkeyStateChanged, m => m.HotkeyId == HotkeyId);
        }

        #region Properties
        public int HotkeyId { get; }

        public List<HotkeyActionOption> HotkeyActionOptions { get; }

        public string Keystroke 
        { 
            get => _keystroke; 
            set 
            {
                if (_keystroke != value)
                    this.RaiseAndSetIfChanged(ref _keystroke, value);
            }
        }

        [Reactive] public bool IsEnabled { get; set; }

        [Reactive] public HotkeyActionOption SelectedActionOption { get; set; }

        [Reactive] public string ErrorKeystroke { get; set; }
        #endregion

        #region Commands 
        public ICommand DeleteCommand
        {
            get => new DelegateCommand
            {
                CommandAction = x =>
                {
                    Task.Run(DeleteHotkey);
                }
            };
        }
        #endregion

        public void UpdateViewModel(Hotkey hotkey)
        {
            var actionOption = HotkeyActionOptions.FirstOrDefault(o => o.Action == hotkey.Action);
            if (actionOption == null)
                actionOption = HotkeyActionOptions.FirstOrDefault(o => o.Action == UserAction.None);
            SelectedActionOption = actionOption;

            IsEnabled = hotkey.Enabled;
            Keystroke = hotkey.Keystroke;
        }

        void OnValueChanged()
        {
            Task.Run(async () =>
            {
                await _metaMessenger.Publish(new ChangeHotkeyMessage(HotkeyId, IsEnabled, SelectedActionOption.Action, Keystroke));
            });
        }

        async Task DeleteHotkey()
        {
            await _metaMessenger.Publish(new DeleteHotkeyMessage(HotkeyId));
        }

        Task OnHotkeyStateChanged(HotkeyStateChangedMessage msg)
        {
            if (msg.State == HotkeyState.Unavailable)
            {
                ErrorKeystroke = TranslationSource.Instance["HotkeysSettings.Keystroke.Unavailable"];
            }
            else if (msg.State == HotkeyState.Subscribed)
                ErrorKeystroke = null;

            return Task.CompletedTask;
        }
    }
}
