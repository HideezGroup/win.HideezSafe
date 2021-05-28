using Hideez.SDK.Communication.Log;
using HideezClient.Messages;
using HideezClient.Modules;
using HideezMiddleware.Localize;
using HideezClient.Modules.Log;
using HideezClient.Modules.ServiceProxy;
using HideezMiddleware.ApplicationModeProvider;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Commands;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Threading;
using HideezClient.Messages.Dialogs;
using HideezClient.Dialogs;

namespace HideezClient.ViewModels
{
    class VaultAccessSettingsControlViewModel : ReactiveObject, IWeakEventListener
    {
        public class TimeoutOption
        {
            public string Title { get; set; }

            public int TimeoutSeconds { get; set; }
        }

        readonly IServiceProxy _serviceProxy;
        readonly IWindowsManager _windowsManager;
        readonly Logger _log = LogManager.GetCurrentClassLogger(nameof(VaultAccessSettingsControlViewModel));
        readonly IMetaPubSub _messenger;
        readonly Version _requiredFirmwareVersion  = new Version(3, 6, 10);

        public VaultAccessSettingsControlViewModel(
            IApplicationModeProvider applicationModeProvider,
            IActiveDevice activeDevice,
            IServiceProxy serviceProxy,
            IWindowsManager windowsManager,
            IMetaPubSub messenger)
        {
            var mode = applicationModeProvider.GetApplicationMode();
            if (mode != ApplicationMode.Standalone)
                return;

            _serviceProxy = serviceProxy;
            _windowsManager = windowsManager;
            _messenger = messenger;

            _messenger.Subscribe<ActiveDeviceChangedMessage>(OnActiveDeviceChanged);

            this.WhenAnyValue(x => x.Device).Where(d => d != null).Subscribe(d =>
            {
                PropertyChangedEventManager.AddListener(Device, this, nameof(Device.IsStorageLoaded));

                Task.Run(TryLoadAccessProfile);
            });

            this.ObservableForProperty(vm => vm.RequirePin).Subscribe(vm => { OnSettingValueChanged(); });
            this.ObservableForProperty(vm => vm.RequireButton).Subscribe(vm => { OnSettingValueChanged(); });
            this.ObservableForProperty(vm => vm.SelectedTimeout).Subscribe(vm => { OnSettingValueChanged(); });

            Device = activeDevice.Device != null ? new DeviceViewModel(activeDevice.Device) : null;
        }

        #region Properties
        [Reactive] public DeviceViewModel Device { get; set; }

        [Reactive] public bool IsLoaded { get; set; }

        [Reactive] public bool IsLoading { get; set; }
        
        [Reactive] public bool IsSupported{ get; set; }

        [Reactive] public bool IsSaving { get; set; }

        [Reactive] public bool HasChanges { get; set; }

        [Reactive]
        public List<TimeoutOption> TimeoutOptionsList { get; set; } = new List<TimeoutOption>
        {
            new TimeoutOption { Title = TranslationSource.Instance["AccessSettings.StorageLockTimeout.NoTimeout"], TimeoutSeconds = 0 },
            new TimeoutOption { Title = TranslationSource.Instance["AccessSettings.StorageLockTimeout.5m"], TimeoutSeconds = 300 },
            new TimeoutOption { Title = TranslationSource.Instance["AccessSettings.StorageLockTimeout.10m"], TimeoutSeconds = 600 },
            new TimeoutOption { Title = TranslationSource.Instance["AccessSettings.StorageLockTimeout.30m"], TimeoutSeconds = 1800 },
            new TimeoutOption { Title = TranslationSource.Instance["AccessSettings.StorageLockTimeout.1h"], TimeoutSeconds = 3600 },
            new TimeoutOption { Title = TranslationSource.Instance["AccessSettings.StorageLockTimeout.2h"], TimeoutSeconds = 7200 },
            new TimeoutOption { Title = TranslationSource.Instance["AccessSettings.StorageLockTimeout.4h"], TimeoutSeconds = 14400 },
            new TimeoutOption { Title = TranslationSource.Instance["AccessSettings.StorageLockTimeout.8h"], TimeoutSeconds = 28800 },
        };

        [Reactive] public bool SavedRequireButton { get; set; }

        [Reactive] public bool SavedRequirePin { get; set; }

        [Reactive] public TimeoutOption SavedSelectedTimeout { get; set; }

        [Reactive] public bool RequireButton { get; set; }

        [Reactive] public bool RequirePin { get; set; }

        [Reactive] public bool IsNewPinRequired { get; set; }

        [Reactive] public TimeoutOption SelectedTimeout { get; set; }
        #endregion

        #region Commands
        public ICommand CancelAccessProfileEditCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(CancelAccessProfileEdit);
                    }
                };
            }
        }

        public ICommand SaveAccessProfileCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(SaveOrUpdateAccessProfile);
                    }
                };
            }
        }

        public ICommand ChangeMasterPasswordCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(ChangeMasterPassword);
                    }
                };
            }
        }

        public ICommand ChangePinCodeCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(async () => 
                        {
                            if (Device.IsNewPinRequired)
                                await SetPin();
                            else
                                await ChangePinCode();
                        });
                    }
                };
            }
        }

        #endregion

        Task OnActiveDeviceChanged(ActiveDeviceChangedMessage obj)
        {
            // Todo: ViewModel should be reused instead of being recreated each time active device is changed
            Device = obj.NewDevice != null ? new DeviceViewModel(obj.NewDevice) : null;
            IsLoaded = false;
            IsLoading = false;
            IsSupported = Device?.FirmwareVersion >= _requiredFirmwareVersion;

            return Task.CompletedTask;
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            // We still receive events from previous device, so this check is important
            // to filter events from device relevant/selected device only
            if (Device != null && Device == sender as DeviceViewModel)
            {
                Task.Run(TryLoadAccessProfile);
            }
            return true;
        }

        void OnSettingValueChanged()
        {
            if (IsLoaded)
                HasChanges = true;
        }

        // Called when device changes or its IsAuthorized property changes
        async Task TryLoadAccessProfile()
        {
            IsLoaded = false;
            HasChanges = false;
            IsSupported = Device?.FirmwareVersion >= _requiredFirmwareVersion;

            if (!IsSupported)
                return;

            if (Device.IsStorageLoaded)
            {
                await LoadAccessProfile();
            }
        }

        async Task LoadAccessProfile()
        {
            try
            {
                IsLoading = true;

                var profile = await Device.GetAccessProfile();

                SavedRequireButton = profile.ButtonReq > 0;
                SavedRequirePin = profile.PinReq > 0;
                if (SavedRequirePin)
                    SavedSelectedTimeout = TimeoutOptionsList.FirstOrDefault(o => o.TimeoutSeconds == profile.PinExpirationPeriod);
                else if (SavedRequireButton)
                    SavedSelectedTimeout = TimeoutOptionsList.FirstOrDefault(o => o.TimeoutSeconds == profile.ButtonExpirationPeriod);
                else
                    SavedSelectedTimeout = TimeoutOptionsList.FirstOrDefault(o => o.TimeoutSeconds == profile.MasterKeyExpirationPeriod);

                RequireButton = SavedRequireButton;
                RequirePin = SavedRequirePin;
                SelectedTimeout = SavedSelectedTimeout;

                IsLoaded = true;
                IsLoading = false;
                HasChanges = false;
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
        }

        void CancelAccessProfileEdit()
        {
            RequireButton = SavedRequireButton;
            RequirePin = SavedRequirePin;
            SelectedTimeout = SavedSelectedTimeout;

            IsLoaded = true;
            HasChanges = false;
        }

        async Task SaveOrUpdateAccessProfile()
        {
            bool isSaved = false;
            try
            {
                IsSaving = true;

                // Temporary, until firmware is updated
                SelectedTimeout = TimeoutOptionsList[5]; // 4h option

                isSaved = await Device.ChangeAccessProfile(RequirePin, RequireButton, SelectedTimeout.TimeoutSeconds);
                if (isSaved)
                {
                    SavedRequireButton = RequireButton;
                    SavedRequirePin = RequirePin;
                    SavedSelectedTimeout = SelectedTimeout;

                    HasChanges = false;
                }
            }
            finally
            {
                IsSaving = false;
            }

            if(isSaved)
                await SetPin();
        }

        async Task SetPin()
        {
            try
            {
                if (Device.IsNewPinRequired)
                    await Device.SetPinCode(CancellationToken.None);
            }
            finally
            {
                await _messenger.Publish(new HideDialogMessage(typeof(PinDialog)));
            }
        }

        async Task ChangeMasterPassword()
        {
            await Device.ChangeMasterPassword();
        }

        async Task ChangePinCode()
        {
            await Device.ChangePinCode();
        }
    }
}
