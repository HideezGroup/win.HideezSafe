using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.PasswordManager;
using HideezClient.Extension;
using HideezClient.Messages;
using HideezClient.Modules;
using HideezClient.Modules.Log;
using HideezClient.Modules.ServiceProxy;
using HideezClient.ViewModels;
using HideezClient.ViewModels.Controls;
using HideezMiddleware.ApplicationModeProvider;
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
using System.Security;
using System.Security.Principal;
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
        bool _proximityHasChanges;
        UserDeviceProximitySettings _oldSettings;

        public DeviceSettingsPageViewModel(
            IServiceProxy serviceProxy,
            IWindowsManager windowsManager,
            IActiveDevice activeDevice,
            IMetaPubSub metaMessenger,
            IApplicationModeProvider applicationModeProvider)
        {
            this.serviceProxy = serviceProxy;
            this.windowsManager = windowsManager;
            _metaMessenger = metaMessenger;

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
            });

            Device = activeDevice.Device != null ? new DeviceViewModel(activeDevice.Device) : null;
            AllowEditProximitySettings = applicationModeProvider.GetApplicationMode() == ApplicationMode.Standalone;
            ProcessResultViewModel = new ProgressIndicatorWithResultViewModel();


            this.WhenAnyValue(x => x.CredentialsHasChanges).Subscribe(o => OnSettingsChanged());

            this.WhenAnyValue(x => x.LockProximity, x => x.UnlockProximity, x => x.EnabledUnlockByProximity,
                x => x.EnabledLockByProximity, x => x.DisabledDisplayAuto).Where(t => t.Item1 != 0 && t.Item2 != 0)
                .Subscribe(o => OnSettingsChanged());

            this.ObservableForProperty(vm => vm.HasChanges).Subscribe(vm => OnHasChangesChanged());
            
            Initialize();
        }

        private async Task Initialize()
        {
            try
            {
                IsLoading = true;

                UserName = GetAccoutName().Split('\\')[1];
                await TryLoadProximitySettings();

                IsLoading = false;
            }
            catch(Exception ex)
            {
                log.WriteLine(ex);
            }
        }

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
        [Reactive] public bool CredentialsHasChanges { get; set; }
        [Reactive] public bool HasChanges { get; set; }
        [Reactive] public bool InProgress { get; set; }
        [Reactive] public bool IsLoading { get; set; }
        [Reactive] public bool AllowEditProximitySettings { get; set; }
        [Reactive] public bool IsEditableCredentials { get; set; }
        [Reactive] public string UserName { get; set; }
        [Reactive] public ProgressIndicatorWithResultViewModel ProcessResultViewModel { get; set; }

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
                    CommandAction = password =>
                    {
                        Task.Run(async()=>await SaveSettings(password as SecureString));
                    }
                };
            }
        }

        public ICommand EditCredentialsCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(() =>
                        {
                            IsEditableCredentials = true;
                            HasChanges = true;
                        });
                    }
                };
            }
        }

        public ICommand CancelEditProximityCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(ResetToPreviousSettings);
                    }
                };
            }
        }

        public ICommand WipeVaultCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(WipeVault);
                    }
                };
            }
        }
        #endregion

        public async Task SaveSettings(SecureString password)
        {
            try
            {
                ProcessResultViewModel.InProgress = true;

                if (CredentialsHasChanges && EnabledUnlockByProximity)
                    await SaveOrUpdateAccount(password);
                if (_proximityHasChanges)
                {
                    if (!EnabledUnlockByProximity && _oldSettings != null && _oldSettings.EnabledUnlockByProximity)
                    {
                        var currentAccount = Device.AccountsRecords.FirstOrDefault(a => a.IsPrimary);
                        if (currentAccount != null)
                            await Device.DeleteAccountAsync(currentAccount);
                    }
                    await SaveOrUpdateSettings();
                }
                ProcessResultViewModel.Result = ProcessResult.Successful;
            }
            catch
            {
                ProcessResultViewModel.Result = ProcessResult.Failed;
            }
            finally
            {
                HasChanges = false;
                IsEditableCredentials = false;
                ProcessResultViewModel.InProgress = false;
            }
        }

        void ResetToPreviousSettings()
        {
            LockProximity = _oldSettings.LockProximity;
            UnlockProximity = _oldSettings.UnlockProximity;
            EnabledLockByProximity = _oldSettings.EnabledLockByProximity;
            EnabledUnlockByProximity = _oldSettings.EnabledUnlockByProximity;
            DisabledDisplayAuto = _oldSettings.DisabledDisplayAuto;

            IsEditableCredentials = false;
            HasChanges = false;
        }

        void OnSettingsChanged()
        {
            if (_oldSettings != null)
            {
                if (LockProximity != _oldSettings.LockProximity || UnlockProximity != _oldSettings.UnlockProximity
                    || EnabledLockByProximity != _oldSettings.EnabledLockByProximity || EnabledUnlockByProximity != _oldSettings.EnabledUnlockByProximity
                    || DisabledDisplayAuto != _oldSettings.DisabledDisplayAuto)
                    _proximityHasChanges = true;
                else _proximityHasChanges = false;

                if (EnabledUnlockByProximity && !_oldSettings.EnabledUnlockByProximity)
                    UpdateIsEditableCredentials();

                HasChanges = CredentialsHasChanges || _proximityHasChanges;
            }
        }

        void OnHasChangesChanged()
        {
            if (HasChanges == true)
                ProcessResultViewModel.Result = ProcessResult.Undefined;
        }

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

                UpdateIsEditableCredentials();
            }
            return true;
        }

        public void UpdateIsEditableCredentials()
        {
            if (Device != null && Device.IsStorageLoaded)
                IsEditableCredentials = !Device.AccountsRecords.Any(r => r.IsPrimary);
        }

        async Task TryLoadProximitySettings()
        {
            try
            {
                if (Device != null)
                {
                    var reply = await _metaMessenger.ProcessOnServer<LoadUserProximitySettingsMessageReply>(new LoadUserProximitySettingsMessage(BleUtils.MacToConnectionId(Device.Mac)));
                    LockProximity = reply.UserDeviceProximitySettings.LockProximity;
                    UnlockProximity = reply.UserDeviceProximitySettings.UnlockProximity;
                    EnabledLockByProximity = reply.UserDeviceProximitySettings.EnabledLockByProximity;
                    EnabledUnlockByProximity = reply.UserDeviceProximitySettings.EnabledUnlockByProximity;
                    DisabledDisplayAuto = reply.UserDeviceProximitySettings.DisabledDisplayAuto;
                    _oldSettings = reply.UserDeviceProximitySettings;
                }
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
                var newSettings = UserDeviceProximitySettings.DefaultSettings;
                newSettings.Id = BleUtils.MacToConnectionId(Device.Mac);
                newSettings.DisabledDisplayAuto = DisabledDisplayAuto;
                newSettings.EnabledLockByProximity = EnabledLockByProximity;
                newSettings.EnabledUnlockByProximity = EnabledUnlockByProximity;
                newSettings.LockProximity = LockProximity;
                newSettings.UnlockProximity = UnlockProximity;

                await _metaMessenger.PublishOnServer(new SaveUserProximitySettingsMessage(newSettings));

                _oldSettings = newSettings;
            }
            catch (Exception ex)
            {
                log.WriteLine($"Failed proximity settings saving: {ex.Message}");
            }
        }

        async Task SaveOrUpdateAccount(SecureString password)
        {
            if (password?.Length != 0)
            {
                var primaryAccount = Device.AccountsRecords.FirstOrDefault(a => a.IsPrimary == true);
                if (primaryAccount == null)
                    primaryAccount = new AccountRecord()
                    {
                        IsPrimary = true,
                    };
                primaryAccount.Name = "Unlock account";
                primaryAccount.Login = GetAccoutName();
                primaryAccount.Password = password.GetAsString();
                await Device.SaveOrUpdateAccountAsync(primaryAccount, true);
            }
        }

        async Task WipeVault()
        {
            await Device.Wipe();
        }

        #region Utils
        private string GetAccoutName()
        {
            var wi = WindowsIdentity.GetCurrent();
            string accountName = wi.Name;
            foreach (var gsid in wi.Groups)
            {
                try
                {
                    var group = new SecurityIdentifier(gsid.Value).Translate(typeof(NTAccount)).ToString();
                    if (group.StartsWith(@"MicrosoftAccount\"))
                    {
                        accountName = group;
                        break;
                    }
                }
                catch (IdentityNotMappedException)
                {
                    // Failed to map SID to NTAccount, skip
                }
                catch (SystemException)
                {
                    // Win32 exception whem mapping SID to NTAccount
                }
            }
            
            return accountName;
        }
        #endregion
    }
}
