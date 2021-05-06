/*
  In App.xaml:
  <Application.Resources>
      <vm:ViewModelLocator xmlns:vm="clr-namespace:WpfApp5"
                           x:Key="Locator" />
  </Application.Resources>
  
  In the View:
  DataContext="{Binding Source={StaticResource Locator}, Path=ViewModelName}"

  You can also use Blend to do all this with the tool's support.
  See http://www.galasoft.ch/mvvm
*/

using CommonServiceLocator;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Ioc;
using HideezClient.Controls;
using HideezClient.Dialogs;
using HideezClient.PageViewModels;
using HideezClient.ViewModels.Controls;
using HideezClient.ViewModels.Controls.UnsupportedControls;
using HideezClient.ViewModels.Dialog;
using System.ComponentModel;
using System.Diagnostics;
using Unity;
using Unity.Lifetime;

namespace HideezClient.ViewModels
{
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    class ViewModelLocator
    {
        private readonly IUnityContainer DIContainer;

        /// <summary>
        /// Initializes a new instance of the ViewModelLocator class.
        /// </summary>
        public ViewModelLocator()
        {
            // Check for design mode. 
            if (IsInDesignMode)
            {
                // in Design mode
                DIContainer = new UnityContainer();
            }
            else
            {
                DIContainer = App.Container;
            }
        }

        public static bool IsInDesignMode
        {
            get { return (bool)(DesignerProperties.IsInDesignModeProperty.GetMetadata(typeof(System.Windows.DependencyObject)).DefaultValue); }
        }

        #region Windows

        public MainViewModel MainViewModel
        {
            get { return DIContainer.Resolve<MainViewModel>(); }
        }

        public NotificationsContainerViewModel NotificationsContainerViewModel
        {
            get { return DIContainer.Resolve<NotificationsContainerViewModel>(); }
        }

        public MainViewModel SimpleMainViewModel
        {
            get { return DIContainer.Resolve<MainViewModel>(); }
        }
        #endregion

        #region Controls

        public IndicatorsViewModel IndicatorsViewModel
        {
            get { return DIContainer.Resolve<IndicatorsViewModel>(); }
        }

        public ServerAddressEditControlViewModel ServerAddressEditControlViewModel
        {
            get { return DIContainer.Resolve<ServerAddressEditControlViewModel>(); }
        }

        public ConnectionModeContainerViewModel ConnectionModeContainerViewModel
        {
            get { return DIContainer.Resolve<ConnectionModeContainerViewModel>(); }
        }

        public AppModeRestrictedContainerViewModel AppModeRestrictedContainerViewModel
        {
            get { return DIContainer.Resolve<AppModeRestrictedContainerViewModel>(); }
        }

        public VaultAccessSettingsControlViewModel VaultAccessSettingsControlViewModel
        {
            get { return DIContainer.Resolve<VaultAccessSettingsControlViewModel>(); }
        }

        public BackupCredentialsControlViewModel BackupCredentialsControlViewModel
        {
            get { return DIContainer.Resolve<BackupCredentialsControlViewModel>(); }
        }

        public UnsupportedAccessSettingsViewModel UnsupportedAccessSettingsViewModel
        {
            get { return DIContainer.Resolve<UnsupportedAccessSettingsViewModel>(); }
        }

        public HotkeySettingsViewModel HotkeySettingsViewModel
        {
            get { return DIContainer.Resolve<HotkeySettingsViewModel>(); }
        }

        #endregion

        #region Dialog

        public PinViewModel PinViewModel
        {
            get { return DIContainer.Resolve<PinViewModel>(); }
        }

        public MasterPasswordViewModel MasterPasswordViewModel
        {
            get { return DIContainer.Resolve<MasterPasswordViewModel>(); }
        }

        public BackupPasswordViewModel BackupPasswordViewModel
        {
            get { return DIContainer.Resolve<BackupPasswordViewModel>(); }
        }

        public DeviceNotAuthorizedNotificationViewModel PinNotVerifiedNotificationViewModel
        {
            get { return DIContainer.Resolve<DeviceNotAuthorizedNotificationViewModel>(); }
        }

        public ActivationViewModel ActivationViewModel
        {
            get { return DIContainer.Resolve<ActivationViewModel>(); }
        }

        public WipeViewModel WipeViewModel
        {
            get { return DIContainer.Resolve<WipeViewModel>(); }
        }

        #endregion

        #region Page

        public DefaultPageViewModel DefaultPageViewModel
        {
            get { return DIContainer.Resolve<DefaultPageViewModel>(); }
        }

        public HardwareKeyPageViewModel HardwareKeyPageViewModel
        {
            get { return DIContainer.Resolve<HardwareKeyPageViewModel>(); }
        }

        public SoftwareKeyPageViewModel SoftwareKeyPageViewModel
        {
            get { return DIContainer.Resolve<SoftwareKeyPageViewModel>(); }
        }

        public HelpPageViewModel HelpPage
        {
            get { return DIContainer.Resolve<HelpPageViewModel>(); }
        }

        public SettingsPageViewModel SettingsPage
        {
            get { return DIContainer.Resolve<SettingsPageViewModel>(); }
        }

        public PasswordManagerViewModel PasswordManager
        {
            get { return DIContainer.Resolve<PasswordManagerViewModel>(); }
        }

        public DeviceSettingsPageViewModel DeviceSettingsPageViewModel
        {
            get { return DIContainer.Resolve<DeviceSettingsPageViewModel>(); }
        }

        #endregion
    }
}
