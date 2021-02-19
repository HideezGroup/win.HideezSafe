﻿using Hideez.SDK.Communication.Log;
using HideezClient.Messages;
using HideezClient.Modules;
using HideezClient.Modules.Log;
using HideezClient.Modules.ServiceProxy;
using HideezClient.ViewModels;
using HideezClient.ViewModels.Controls;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.Settings;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Commands;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HideezClient.PageViewModels
{
    class DeviceSettingsPageViewModel : ReactiveObject, IWeakEventListener
    {
        readonly IServiceProxy serviceProxy;
        readonly IWindowsManager windowsManager;
        readonly Logger log = LogManager.GetCurrentClassLogger(nameof(DeviceSettingsPageViewModel));
        readonly IMetaPubSub _metaMessenger;

        public DeviceSettingsPageViewModel(
            IServiceProxy serviceProxy,
            IWindowsManager windowsManager,
            IActiveDevice activeDevice,
            IMetaPubSub metaMessenger)
        {
            this.serviceProxy = serviceProxy;
            this.windowsManager = windowsManager;
            _metaMessenger = metaMessenger;

            VaultAccessSettingsViewModel = new VaultAccessSettingsViewModel(serviceProxy, windowsManager, _metaMessenger);

            _metaMessenger.Subscribe<ActiveDeviceChangedMessage>(OnActiveDeviceChanged);

            Сonnected = new StateControlViewModel
            {
                Name = "Status.Device.Сonnected",
                Visible = true,
            };
            Initialized = new StateControlViewModel
            {
                Name = "Status.Device.Initialized",
                Visible = true,
            };
            Authorized = new StateControlViewModel
            {
                Name = "Status.Device.Authorized",
                Visible = true,
            };
            StorageLoaded = new StateControlViewModel
            {
                Name = "Status.Device.StorageLoaded",
                Visible = true,
            };

            Indicators.Add(Сonnected);
            Indicators.Add(Initialized);
            Indicators.Add(Authorized);
            Indicators.Add(StorageLoaded);

            this.WhenAnyValue(x => x.Device).Where(d => d != null).Subscribe(d =>
            {
                PropertyChangedEventManager.AddListener(Device, this, nameof(Device.IsConnected));
                PropertyChangedEventManager.AddListener(Device, this, nameof(Device.IsInitialized));
                PropertyChangedEventManager.AddListener(Device, this, nameof(Device.IsAuthorized));
                PropertyChangedEventManager.AddListener(Device, this, nameof(Device.IsStorageLoaded));

                Сonnected.State = StateControlViewModel.BoolToState(Device.IsConnected);
                Initialized.State = StateControlViewModel.BoolToState(Device.IsInitialized);
                Authorized.State = StateControlViewModel.BoolToState(Device.IsAuthorized);
                StorageLoaded.State = StateControlViewModel.BoolToState(Device.IsStorageLoaded);

                VaultAccessSettingsViewModel.Device = Device;
            });

            this.WhenAnyValue(x => x.LockProximity, x => x.UnlockProximity).Where(t => t.Item1 != 0 && t.Item2 != 0).Subscribe(o => ProximityHasChanges = true);

            Device = activeDevice.Device != null ? new DeviceViewModel(activeDevice.Device) : null;

            TryLoadProximitySettings();
        }

        [Reactive] public VaultAccessSettingsViewModel VaultAccessSettingsViewModel { get; set; }
        [Reactive] public DeviceViewModel Device { get; set; }
        [Reactive] public StateControlViewModel Сonnected { get; set; }
        [Reactive] public StateControlViewModel Initialized { get; set; }
        [Reactive] public StateControlViewModel Authorized { get; set; }
        [Reactive] public StateControlViewModel StorageLoaded { get; set; }
        [Reactive] public int LockProximity { get; set; }
        [Reactive] public int UnlockProximity { get; set; }
        [Reactive] public bool EnabledLockByProximity { get; set; }
        [Reactive] public bool EnabledUnlockByProximity { get; set; }
        [Reactive] public bool DisabledDisplayAuto { get; set; }
        [Reactive] public bool ProximityHasChanges { get; set; }
        [Reactive] public bool AllowEditProximitySettings { get; set; }


        public ObservableCollection<StateControlViewModel> Indicators { get; } = new ObservableCollection<StateControlViewModel>();

        #region Command

        public ICommand SelectCSVFileCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {

                    }
                };
            }
        }

        public ICommand ExportCredentialsCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {

                    }
                };
            }
        }

        public ICommand SaveProximityCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        SaveOrUpdateSettings();
                    }
                };
            }
        }

        #endregion

        private async Task OnActiveDeviceChanged(ActiveDeviceChangedMessage obj)
        {
            // Todo: ViewModel should be reused instead of being recreated each time active device is changed
            Device = obj.NewDevice != null ? new DeviceViewModel(obj.NewDevice) : null;

            await TryLoadProximitySettings();
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            // We still receive events from previous device, so this check is important
            // to filter events from device relevant/selected device only
            if (Device != null && Device == sender as DeviceViewModel)
            {
                Сonnected.State = StateControlViewModel.BoolToState(Device.IsConnected);
                Initialized.State = StateControlViewModel.BoolToState(Device.IsInitialized);
                Authorized.State = StateControlViewModel.BoolToState(Device.IsAuthorized);
                StorageLoaded.State = StateControlViewModel.BoolToState(Device.IsStorageLoaded);
            }
            return true;
        }

        async Task TryLoadProximitySettings()
        {
            try
            {
                string connectionId = Device.Id.Remove(Device.Id.Length - 2);
                var reply = await _metaMessenger.ProcessOnServer<LoadUserProximitySettingsMessageReply>(new LoadUserProximitySettingsMessage(connectionId));
                LockProximity = reply.UserDeviceProximitySettings.LockProximity;
                UnlockProximity = reply.UserDeviceProximitySettings.UnlockProximity;
                EnabledLockByProximity = reply.UserDeviceProximitySettings.EnabledLockByProximity;
                EnabledUnlockByProximity = reply.UserDeviceProximitySettings.EnabledUnlockByProximity;
                DisabledDisplayAuto = reply.UserDeviceProximitySettings.DisabledDisplayAuto;
            }
            catch(Exception ex)
            {
                log.WriteLine($"Failed proximity settings loading: {ex.Message}");
            }
        }

        async Task SaveOrUpdateSettings()
        {
            try
            {
                string connectionId = Device.Id.Remove(Device.Id.Length - 2);

                var newSettings = UserDeviceProximitySettings.DefaultSettings;
                newSettings.Id = connectionId;
                newSettings.DisabledDisplayAuto = DisabledDisplayAuto;
                newSettings.EnabledLockByProximity = EnabledLockByProximity;
                newSettings.EnabledUnlockByProximity = EnabledUnlockByProximity;
                newSettings.LockProximity = LockProximity;
                newSettings.UnlockProximity = UnlockProximity;

                await _metaMessenger.PublishOnServer(new SaveUserProximitySettingsMessage(newSettings));
            }
            catch (Exception ex)
            {
                log.WriteLine($"Failed proximity settings saving: {ex.Message}");
            }
        }
    }
}
