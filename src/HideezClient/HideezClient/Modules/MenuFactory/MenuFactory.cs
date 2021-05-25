using GalaSoft.MvvmLight.Messaging;
using HideezClient.Models.Settings;
using HideezMiddleware.Localize;
using HideezClient.Properties;
using HideezClient.Utilities;
using HideezClient.ViewModels;
using MvvmExtensions.Commands;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using NLog;
using System.IO;
using NLog.Layouts;
using System.Threading.Tasks;
using HideezClient.Models;
using HideezClient.Modules.ServiceProxy;
using System.ServiceModel;
using HideezMiddleware.Settings;
using HideezClient.Mvvm;
using HideezClient.Messages;
using Meta.Lib.Modules.PubSub;
using HideezMiddleware.IPC.IncommingMessages;

namespace HideezClient.Modules
{
    class MenuFactory : IMenuFactory
    {
        readonly IStartupHelper _startupHelper;
        readonly IWindowsManager _windowsManager;
        readonly IAppHelper _appHelper;
        readonly ISettingsManager<ApplicationSettings> _settingsManager;
        readonly ISupportMailContentGenerator _supportMailContentGenerator;
        readonly IServiceProxy _serviceProxy;
        readonly IActiveDevice _activeDevice;
        readonly IMetaPubSub _metaMessenger;

        public MenuFactory(IStartupHelper startupHelper
            , IWindowsManager windowsManager, IAppHelper appHelper,
            ISettingsManager<ApplicationSettings> settingsManager, ISupportMailContentGenerator supportMailContentGenerator,
            IServiceProxy serviceProxy, IActiveDevice activeDevice, IMetaPubSub metaMessenger)
        {
            _startupHelper = startupHelper;
            _windowsManager = windowsManager;
            _appHelper = appHelper;
            _settingsManager = settingsManager;
            _supportMailContentGenerator = supportMailContentGenerator;
            _serviceProxy = serviceProxy;
            _activeDevice = activeDevice;
            _metaMessenger = metaMessenger;
        }

        public MenuItemViewModel GetMenuItem(MenuItemType type)
        {
            switch (type)
            {
                case MenuItemType.ShowWindow:
                    return GetViewModel("Menu.ShowWindow", x => _windowsManager.ActivateMainWindow());
                case MenuItemType.AddDevice:
                    return GetViewModel("Menu.AddDevice", x => throw new NotImplementedException());
                case MenuItemType.CheckForUpdates:
                    return GetViewModel("Menu.CheckForUpdates", x => throw new NotImplementedException());
                case MenuItemType.ChangePassword:
                    return GetViewModel("Menu.ChangePassword", x => throw new NotImplementedException());
                case MenuItemType.UserManual:
                    return GetViewModel("Menu.UserManual", x => OnOpenUrl("Url.UserManual"));
                case MenuItemType.TechnicalSupport:
                    return GetViewModel("Menu.TechnicalSupport", x => Task.Run(() => OnTechSupportAsync("SupportMail")));
                case MenuItemType.LiveChat:
                    return GetViewModel("Menu.LiveChat", x => OnOpenUrl("Url.LiveChat"));
                case MenuItemType.Legal:
                    return GetViewModel("Menu.Legal", x => OnOpenUrl("Url.Legal"));
                case MenuItemType.RFIDUsage:
                    return GetViewModel("Menu.RFIDUsage", x => OnOpenUrl("Url.RFIDUsage"));
                case MenuItemType.VideoTutorial:
                    return GetViewModel("Menu.VideoTutorial", x => OnOpenUrl("Url.VideoTutorial"));
                case MenuItemType.LogOff:
                    return GetViewModel("Menu.LogOff", x => throw new NotImplementedException());
                case MenuItemType.Exit:
                    return GetViewModel("Menu.Exit", x => _appHelper.Shutdown());
                case MenuItemType.Language:
                    return GetLanguages();
                case MenuItemType.LaunchOnStartup:
                    return GetLaunchOnStartup();
                case MenuItemType.GetLogsSubmenu:
                    return GetLogsSubmenu();
                case MenuItemType.Help:
                    return GetViewModel("Menu.Help", x => OnOpenUrl("Url.Help"));
                case MenuItemType.Separator:
                    return null;
                default:
                    Debug.Assert(false, $"The type: {type} of menu is not supported.");
                    return null;
            }
        }

        public MenuItemViewModel GetMenuItem(DeviceModel device, MenuItemType type)
        {
            if (device == null)
            {
                Debug.Assert(false, "Vault can not be null.");
            }

            switch (type)
            {
                case MenuItemType.DisconnectDevice:
                    return GetMenuDisconnectDevice(device);
                case MenuItemType.RemoveDevice:
                    return GetMenuRemoveDevice(device);
                case MenuItemType.AuthorizeDeviceAndLoadStorage:
                    return GetMenuAuthorizeAndLoadStorage(device);
                case MenuItemType.AboutDevice:
                    return null; // TODO: About device menu
                case MenuItemType.SetAsActiveDevice:
                    return GetMenuSetAsActiveDevice(device);
                default:
                    Debug.Assert(false, $"The type: {type} of menu is not supported.");
                    return null;
            }
        }

        private MenuItemViewModel GetMenuRemoveDevice(DeviceModel device)
        {
            return new MenuItemViewModel
            {
                Header = "Menu.RemoveDevice.Tooltip",
                Command = new DelegateCommand
                {
                    CommandAction = (x) => OnRemoveDevice(device),
                },
            };
        }

        private MenuItemViewModel GetMenuDisconnectDevice(DeviceModel device)
        {
            return new MenuItemViewModel
            {
                Header = "Menu.DisconnectDevice.Tooltip",
                Command = new DelegateCommand
                {
                    CanExecuteFunc = () => device.IsConnected,
                    CommandAction = (x) => OnDisconnectDevice(device),
                },
            };
        }

        private MenuItemViewModel GetMenuAuthorizeAndLoadStorage(DeviceModel device)
        {
            return new MenuItemViewModel
            {
                Header = "Menu.Authorize.Tooltip",
                Command = new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    (device.FinishedMainFlow &&
                    !device.IsAuthorized &&
                    !device.IsAuthorizingRemoteDevice &&
                    !device.IsCreatingRemoteDevice) 
                    ||
                    (device.FinishedMainFlow &&
                    device.IsAuthorized &&
                    !device.IsStorageLoaded &&
                    !device.IsLoadingStorage &&
                    !device.IsCreatingRemoteDevice),

                    CommandAction = (x) =>
                    {
                        OnAuthorizeAndLoadStorage(device);
                    }
                },
            };
        }

        private MenuItemViewModel GetMenuSetAsActiveDevice(DeviceModel device)
        {
            return new MenuItemViewModel
            {
                Header = "Menu.SetAsActive.ToolTip",
                Command = new DelegateCommand
                {
                    CommandAction = (x) =>
                    {
                        OnChangeActiveDevice(device);
                    }
                },
            };
        }

        private async void OnDisconnectDevice(DeviceModel device)
        {
            try
            {
                var result = await _windowsManager.ShowDisconnectDevicePromptAsync(device.Name);

                if (result)
                    await _metaMessenger.PublishOnServer(new DisconnectDeviceMessage(device.Id));
            }
            catch (Exception ex)
            {
                await _metaMessenger.Publish(new ShowErrorNotificationMessage(ex.Message, notificationId: device.Mac));
            }

        }

        private async void OnRemoveDevice(DeviceModel device)
        {
            try
            {
                var result = await _windowsManager.ShowRemoveDevicePromptAsync(device.Name);

                if (result)
                    await _metaMessenger.PublishOnServer(new RemoveDeviceMessage(device.Id));
            }
            catch (Exception ex)
            {
                await _metaMessenger.Publish(new ShowErrorNotificationMessage(ex.Message, notificationId: device.Mac));
            }
        }

        private async void OnAuthorizeAndLoadStorage(DeviceModel device)
        {
            await device.InitRemoteAndLoadStorageAsync();
        }

        private async void OnChangeActiveDevice(DeviceModel device)
        {
            _activeDevice.Device = device;
            await device.InitRemoteAndLoadStorageAsync();
        }

        private async Task OnTechSupportAsync(string techSupportUriKey)
        {
            string techSupportUri = TranslationSource.Instance[techSupportUriKey];
            var mail = await _supportMailContentGenerator.GenerateSupportMail(techSupportUri);
            _appHelper.OpenUrl(mail);
        }

        private void OnOpenUrl(string urlKey)
        {
            string url = TranslationSource.Instance[urlKey];
            _appHelper.OpenUrl(url);
        }

        private MenuItemViewModel GetLaunchOnStartup()
        {
            MenuItemViewModel vm = GetViewModel("Menu.LaunchOnStartup", x => _startupHelper.ReverseState());
            vm.IsCheckable = true;
            vm.CommandParameter = vm;
            vm.IsChecked = _startupHelper.IsInStartup();
            // TODO add weak event handler
            _startupHelper.StateChanged += (appName, state) => vm.IsChecked = (state == AutoStartupState.On);
            return vm;
        }

        private MenuItemViewModel GetLanguages()
        {
            var menuLenguage = new MenuItemViewModel { Header = "Menu.InterfaceLanguages", };
            menuLenguage.MenuItems = new ObservableCollection<MenuItemViewModel>();

            // supported cultures
            CultureInfo defaultCulture = new CultureInfo(_settingsManager.Settings.SelectedUiLanguage);
            foreach (CultureInfo culture in TranslationSource.Instance.SupportedCultures)
            {
                try
                {
                    char[] arrName = culture.NativeName.ToCharArray();
                    arrName[0] = Char.ToUpper(arrName[0], culture);

                    var menuItem = new LanguageMenuItemViewModel
                    {
                        Header = new string(arrName),
                        IsCheckable = true,
                        IsChecked = culture.Equals(defaultCulture),
                        CommandParameter = culture,
                    };
                    menuItem.Command = new DelegateCommand
                    {
                        CommandAction = x =>
                        {
                            if (x is CultureInfo cultureInfo)
                                OnApplyLanguage(cultureInfo);
                        }
                    };
                    menuLenguage.MenuItems.Add(menuItem);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    Debug.Assert(false);
                }
            }

            return menuLenguage;
        }

        private void OnApplyLanguage(CultureInfo cultureInfo)
        {
            _appHelper.ChangeCulture(cultureInfo);
        }

        private MenuItemViewModel GetViewModel(string header, Action<object> action)
        {
            return new MenuItemViewModel
            {
                Header = header,
                Command = new DelegateCommand
                {
                    CommandAction = action,
                }
            };
        }

        private MenuItemViewModel GetLogsSubmenu()
        {
            var logsMenu = new MenuItemViewModel { Header = "Menu.Logs", };
            logsMenu.MenuItems = new ObservableCollection<MenuItemViewModel>();

            try
            {
                var openClientLogsFolderItem = new MenuItemViewModel
                {
                    Header = "Menu.Logs.OpenClientFolder",
                    Command = new DelegateCommand
                    {
                        CommandAction = x =>
                        {
                            try
                            {
                                var logsPath = LogManager.Configuration.Variables["logDir"].Text;
                                var fullPath = LogManagement.GetTargetFolder(logsPath);
                                Process.Start(fullPath);
                            }
                            catch (Exception) { }
                        }
                    }
                };
                var openServiceLogsFolderItem = new MenuItemViewModel
                {
                    Header = "Menu.Logs.OpenServiceFolder",
                    Command = new DelegateCommand
                    {
                        CommandAction = x =>
                        {
                            try
                            {
                                var logsPath = LogManager.Configuration.Variables["serviceLogDir"].Text;
                                var fullPath = LogManagement.GetTargetFolder(logsPath);
                                Process.Start(fullPath);
                            }
                            catch (Exception) { }
                        }
                    }
                };
                var openDeviceLogsFolderItem = new MenuItemViewModel
                {
                    Header = "Menu.Logs.OpenDeviceFolder",
                    Command = new DelegateCommand
                    {
                        CommandAction = x =>
                        {
                            try
                            {
                                var logsPath = LogManager.Configuration.Variables["deviceLogDir"].Text;
                                var fullPath = LogManagement.GetTargetFolder(logsPath);
                                Process.Start(fullPath);
                            }
                            catch (Exception) { }
                        }
                    }
                };

                logsMenu.MenuItems.Add(openClientLogsFolderItem);
                logsMenu.MenuItems.Add(openServiceLogsFolderItem);
                logsMenu.MenuItems.Add(openDeviceLogsFolderItem);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Debug.Assert(false);
            }

            return logsMenu;
        }
    }
}
