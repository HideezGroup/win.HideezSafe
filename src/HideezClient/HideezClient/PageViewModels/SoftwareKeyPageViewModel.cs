using Hideez.SDK.Communication.Workstation;
using HideezClient.Extension;
using HideezClient.Mvvm;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.SoftwareVault.QrFactories;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Commands;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace HideezClient.PageViewModels
{
    class SoftwareKeyPageViewModel : LocalizedObject
    {
        readonly ActivationQrBitmapFactory _activationQrBitmapFactory;
        private readonly IMetaPubSub _metaMessenger;
        BitmapImage _activationQR;
        bool _isEnabled = false;

        public BitmapImage ActivationQR
        {
            get { return _activationQR; }
            set { Set(ref _activationQR, value); }
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { Set(ref _isEnabled, value); }
        }

        public ICommand ShowActivationCodeCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (x) => Task.Run(OnShowActivationCode),
                };
            }
        }

        private async Task OnShowActivationCode()
        {
            try
            {
                await _metaMessenger.PublishOnServer(new SetSoftwareVaultUnlockModuleStateMessage(true));
            }
            catch (Exception) { }
        }

        public SoftwareKeyPageViewModel(IWorkstationInfoProvider workstationInfoProvider, IMetaPubSub metaMessenger)
        {
            _metaMessenger = metaMessenger;
            _activationQrBitmapFactory = new ActivationQrBitmapFactory(workstationInfoProvider);

            _metaMessenger.TrySubscribeOnServer<ServiceSettingsChangedMessage>(OnServiceSettingsChanged);
            try
            {
                _metaMessenger.PublishOnServer(new RefreshServiceInfoMessage());
            }
            catch (Exception) { }
        }

        Task OnServiceSettingsChanged(ServiceSettingsChangedMessage arg)
        {
            if (IsEnabled != arg.SoftwareVaultUnlockEnabled)
            {
                IsEnabled = arg.SoftwareVaultUnlockEnabled;
                if (IsEnabled)
                    GenerateActivationQr();
            }
            return Task.CompletedTask;
        }

        void GenerateActivationQr()
        {
            var qrBitmap = _activationQrBitmapFactory.GenerateActivationQrBitmap();
            Application.Current.Dispatcher.Invoke(() => { ActivationQR = qrBitmap.ToBitmapImage(); });
        }
    }
}
