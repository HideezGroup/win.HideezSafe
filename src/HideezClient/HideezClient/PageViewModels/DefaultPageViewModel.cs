using GalaSoft.MvvmLight.Messaging;
using HideezClient.Messages;
using HideezClient.Mvvm;
using HideezClient.ViewModels;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Commands;
using System.Windows.Input;

namespace HideezClient.PageViewModels
{
    class DefaultPageViewModel : LocalizedObject
    {
        readonly IMetaPubSub _metaMessenger;
        readonly SoftwareUnlockSettingViewModel _softwareUnlock;

        public ICommand OpenHideezKeyPageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (x) => OnOpenHideezKeyPage(),
                };
            }
        }

        public ICommand OpenMobileAuthenticatorPageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (x) => OnOpenMobileAuthenticatorPage(),
                };
            }
        }

        public DefaultPageViewModel(IMetaPubSub metaMessenger, SoftwareUnlockSettingViewModel softwareUnlock)
        {
            _metaMessenger = metaMessenger;
            _softwareUnlock = softwareUnlock;
        }

        void OnOpenHideezKeyPage()
        {
            _metaMessenger.Publish(new OpenHideezKeyPageMessage());
        }

        void OnOpenMobileAuthenticatorPage()
        {
            _metaMessenger.Publish(new OpenMobileAuthenticatorPageMessage());
        }
    }
}
