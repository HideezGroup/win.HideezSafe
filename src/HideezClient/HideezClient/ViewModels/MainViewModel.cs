using HideezClient.Messages;
using HideezClient.Modules.DeviceManager;
using HideezClient.Mvvm;
using MvvmExtensions.Commands;
using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Windows.Input;
using System.Linq;
using HideezClient.Modules;
using System.Collections.Generic;
using MvvmExtensions.Attributes;
using HideezClient.Utilities;
using System.Threading.Tasks;
using Meta.Lib.Modules.PubSub;
using HideezClient.ViewModels.Controls;
using HideezMiddleware.Modules.UpdateCheck.Messages;
using HideezMiddleware.ApplicationModeProvider;

namespace HideezClient.ViewModels
{
    class MainViewModel : ObservableObject
    {
        string currentPage = "";
        readonly IDeviceManager _deviceManager;
        readonly IMenuFactory _menuFactory;
        readonly IActiveDevice _activeDevice;
        readonly IMetaPubSub _messenger;
        readonly ViewModelLocator _viewModelLocator;
        readonly IAppHelper _appHelper;
        readonly SoftwareUnlockSettingViewModel _softwareUnlock;
        readonly IApplicationModeProvider _applicationModeProvider;
        readonly ISet<MenuItemViewModel> _leftAppMenuItems = new HashSet<MenuItemViewModel>();
        readonly ISet<MenuItemViewModel> _leftDeviceMenuItems = new HashSet<MenuItemViewModel>();
        Uri _displayPage;
        DeviceInfoViewModel _activeDeviceVM = null;

        public MainViewModel(IDeviceManager deviceManager,
            IMenuFactory menuFactory,
            IActiveDevice activeDevice,
            IMetaPubSub metaMessenger,
            ViewModelLocator viewModelLocator,
            IAppHelper appHelper,
            SoftwareUnlockSettingViewModel softwareUnlock,
            IApplicationModeProvider applicationModeProvider)
        {
            _deviceManager = deviceManager;
            _menuFactory = menuFactory;
            _activeDevice = activeDevice;
            _viewModelLocator = viewModelLocator;
            _appHelper = appHelper;
            _softwareUnlock = softwareUnlock;
            _applicationModeProvider = applicationModeProvider;

            _messenger = metaMessenger;

            InitMenu();

            _deviceManager.DevicesCollectionChanged += Devices_CollectionChanged;
            _activeDevice.ActiveDeviceChanged += ActiveDevice_ActiveDeviceChanged;

            _messenger.Subscribe<OpenPasswordManagerMessage>((p) => { MenuPasswordManager.IsChecked = true; return Task.CompletedTask; });
            _messenger.Subscribe<OpenHideezKeyPageMessage>((p) => { MenuHardwareKeyPage.IsChecked = true; return Task.CompletedTask; });
            _messenger.Subscribe<OpenMobileAuthenticatorPageMessage>((p) => { MenuSoftwareKeyPage.IsChecked = true; return Task.CompletedTask; });
            _messenger.Subscribe<ApplicationUpdateAvailableMessage>((p) => { MenuUpdateAvailable.IsVisible = true; return Task.CompletedTask; });
        }

        void InitMenu()
        {
            MenuDeviceSettings = new MenuItemViewModel
            {
                Header = "Menu.DeviceSettings",
                Command = OpenDeviceSettingsPageCommand,
            };
            MenuPasswordManager = new MenuItemViewModel
            {
                Header = "Menu.PasswordManager",
                Command = OpenPasswordManagerPageCommand,
            };
            _leftDeviceMenuItems.Add(MenuDeviceSettings);
            _leftDeviceMenuItems.Add(MenuPasswordManager);

            MenuDefaultPage = new MenuItemViewModel
            {
                Header = "Menu.MenuDefaultPage",
                Command = OpenDefaultPageCommand,
                IsChecked = true,
            };
            MenuHelp = new MenuItemViewModel
            {
                Header = "Menu.Help",
                Command = _menuFactory.GetMenuItem(MenuItemType.Help).Command,
            };
            MenuSettings = new MenuItemViewModel
            {
                Header = "Menu.Settings",
                Command = OpenSettingsPageCommand,
            };
            MenuHardwareKeyPage = new MenuItemViewModel
            {
                Header = "Menu.HardwareKeyPage.Header",
                Description = "Menu.HardwareKeyPage.Description",
                Command = OpenHardwareKeyPageCommand,
            };
            MenuSoftwareKeyPage = new MenuItemViewModel
            {
                Header = "Menu.SoftwareKeyPage.Header",
                Description = "Menu.SoftwareKeyPage.Description",
                Command = OpenSoftwareKeyPageCommand,
            };
            MenuUpdateAvailable = new MenuItemViewModel
            {
                Header = "Menu.UpdateAvailable",
                Command = OpenUpdateAvailablePageCommand,
                IsVisible = false,
            };

            _leftAppMenuItems.Add(MenuDefaultPage);
            _leftAppMenuItems.Add(MenuHelp);
            _leftAppMenuItems.Add(MenuSettings);
            _leftAppMenuItems.Add(MenuUpdateAvailable);
            _leftAppMenuItems.Add(MenuHardwareKeyPage);
            _leftAppMenuItems.Add(MenuSoftwareKeyPage);

            foreach (var item in _leftDeviceMenuItems.Concat(_leftAppMenuItems))
            {
                item.PropertyChanged += Item_PropertyChanged;
            }
        }

        void Item_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MenuItemViewModel.IsChecked) && sender is MenuItemViewModel menu && menu.IsChecked)
            {
                foreach (var item in _leftDeviceMenuItems.Concat(_leftAppMenuItems).Where(m => m != menu && m.IsChecked))
                {
                    item.IsChecked = false;
                }
            }
        }

        void Devices_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_deviceManager.Devices.Count() == 0)
            {
                MenuDefaultPage.IsChecked = true;
            }

            NotifyPropertyChanged(nameof(Devices));
        }

        void ActiveDevice_ActiveDeviceChanged(object sender, ActiveDeviceChangedEventArgs args)
        {
            if (ActiveDevice != null)
            {
                ActiveDevice.PropertyChanged -= ActiveDevice_PropertyChanged;
                ActiveDevice = null;

                MenuDefaultPage.IsChecked = true;
            }

            if (args.NewDevice != null)
            {
                ActiveDevice = new DeviceInfoViewModel(args.NewDevice, _menuFactory);
                ActiveDevice.PropertyChanged += ActiveDevice_PropertyChanged;

                MenuDeviceSettings.IsChecked = true;
            }
        }

        void ActiveDevice_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeviceInfoViewModel.CanAccessStorage))
            {
                if (ActiveDevice != null && !ActiveDevice.CanAccessStorage && MenuPasswordManager.IsChecked)
                {
                    MenuDeviceSettings.IsChecked = true;
                }
            }

            if (_applicationModeProvider.GetApplicationMode() == ApplicationMode.Enterprise &&
                e.PropertyName == nameof(DeviceInfoViewModel.IsStorageLoaded))
            {
                if (ActiveDevice != null && ActiveDevice.IsStorageLoaded && ActiveDevice.CanAccessStorage)
                {
                    MenuPasswordManager.IsChecked = true;
                }
            }
        }

        #region Properties

        public string Version
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public Uri DisplayPage
        {
            get { return _displayPage; }
            set { Set(ref _displayPage, value); }
        }

        public DeviceInfoViewModel ActiveDevice
        {
            get { return _activeDeviceVM; }
            set { Set(ref _activeDeviceVM, value); }
        }

        [DependsOn(nameof(ActiveDevice))]
        public List<DeviceInfoViewModel> Devices
        {
            get
            {
                // Todo: cache ViewModels instead of recreating them each time the device collection changes.
                return _deviceManager.Devices
                    .Where(d => d.Id != _activeDevice.Device.Id)
                    .Select(v => new DeviceInfoViewModel(v, _menuFactory))
                    .Reverse()
                    .ToList();
            }
        }
        #endregion Properties

        #region MenuItems

        private MenuItemViewModel menuPasswordManager;
        private MenuItemViewModel menuHelp;
        private MenuItemViewModel menuSettings;
        private MenuItemViewModel menuAboutDevice;
        private MenuItemViewModel menuDefaultPage;
        private MenuItemViewModel menuHardwareKeyPage;
        private MenuItemViewModel menuSoftwareKeyPage;
        private MenuItemViewModel menuUpdateAvailablePage;

        public MenuItemViewModel MenuPasswordManager
        {
            get { return menuPasswordManager; }
            set { Set(ref menuPasswordManager, value); }
        }

        public MenuItemViewModel MenuHelp
        {
            get { return menuHelp; }
            set { Set(ref menuHelp, value); }
        }

        public MenuItemViewModel MenuSettings
        {
            get { return menuSettings; }
            set { Set(ref menuSettings, value); }
        }

        public MenuItemViewModel MenuDeviceSettings
        {
            get { return menuAboutDevice; }
            set { Set(ref menuAboutDevice, value); }
        }

        public MenuItemViewModel MenuDefaultPage
        {
            get { return menuDefaultPage; }
            set { Set(ref menuDefaultPage, value); }
        }

        public MenuItemViewModel MenuHardwareKeyPage
        {
            get { return menuHardwareKeyPage; }
            set { Set(ref menuHardwareKeyPage, value); }
        }

        public MenuItemViewModel MenuSoftwareKeyPage
        {
            get { return menuSoftwareKeyPage; }
            set { Set(ref menuSoftwareKeyPage, value); }
        }

        public MenuItemViewModel MenuUpdateAvailable
        {
            get { return menuUpdateAvailablePage; }
            set { Set(ref menuUpdateAvailablePage, value); }
        }

        #endregion

        #region Command

        public ICommand OpenPasswordManagerPageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        OnOpenPasswordManager();
                    }
                };
            }
        }

        public ICommand OpenSettingsPageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x => OnOpenSettings(),
                };
            }
        }

        public ICommand OpenWinBlePageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x => OnOpenWinBle(),
                };
            }
        }

        public ICommand OpenDeviceSettingsPageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        OnOpenDeviceSettingsPage();
                    },
                };
            }
        }

        public ICommand OpenDefaultPageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        OnOpenDefaultPage();
                    },
                };
            }
        }

        public ICommand OpenHardwareKeyPageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        OnOpenHardwareKeyPage();
                    },
                };
            }
        }

        public ICommand OpenSoftwareKeyPageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        OnOpenSoftwareKeyPage();
                    },
                };
            }
        }

        public ICommand OpenUpdateAvailablePageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        OnOpenUpdateAvailablePage();
                    },
                };
            }
        }
        #endregion Command

        #region Navigation

        public void ProcessNavRequest(string page)
        {
            if (currentPage != page)
            {
                DisplayPage = new Uri($"pack://application:,,,/HideezClient;component/PagesView/{page}.xaml", UriKind.Absolute);
                currentPage = page;
            }
        }

        private void OnOpenSettings()
        {
            ProcessNavRequest("SettingsPage");
        }

        private void OnOpenWinBle()
        {
            ProcessNavRequest("WinBlePairPage");
        }

        private void OnOpenPasswordManager()
        {
            ProcessNavRequest("PasswordManagerPage");
        }

        private void OnOpenDefaultPage()
        {
            ProcessNavRequest("DefaultPage");
        }

        private void OnOpenDeviceSettingsPage()
        {
            ProcessNavRequest("DeviceSettingsPage");
        }

        private void OnOpenHardwareKeyPage()
        {
            ProcessNavRequest("HardwareKeyPage");
        }

        private void OnOpenSoftwareKeyPage()
        {
            ProcessNavRequest("SoftwareKeyPage");

            // Todo: Temporary for Try&Buy
            _softwareUnlock.IsChecked = true;
        }

        private void OnOpenUpdateAvailablePage()
        {
            Task.Run(async () =>
            {
                try
                {
                    await _messenger.Publish(new StartApplicationUpdateMessage());
                }
                catch (Exception)
                { 
                }
            });
        }

        #endregion
    }
}
