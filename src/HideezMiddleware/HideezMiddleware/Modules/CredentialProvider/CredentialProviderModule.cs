using Hideez.SDK.Communication.Log;
using HideezMiddleware.CredentialProvider;
using HideezMiddleware.Modules.CredentialProvider.Messages;
using Meta.Lib.Modules.PubSub;
using System;

namespace HideezMiddleware.Modules.CredentialProvider
{
    public sealed class CredentialProviderModule : ModuleBase
    {
        readonly CredentialProviderProxy _credentialProviderProxy;
        readonly StatusManager _statusManager;

        public CredentialProviderModule(CredentialProviderProxy credentialProviderProxy, 
            StatusManager statusManager, 
            IMetaPubSub messenger, 
            ILog log)
            : base(messenger, nameof(CredentialProviderModule), log)
        {
            _credentialProviderProxy = credentialProviderProxy;
            _statusManager = statusManager;

            _credentialProviderProxy.ProviderActivated += CredentialProviderProxy_ProviderActivated;
            _credentialProviderProxy.CommandLinkPressed += CredentialProviderProxy_CommandLinkPressed;
            _credentialProviderProxy.Connected += CredentialProviderProxy_Connected;
            _credentialProviderProxy.Start();
        }

        private async void CredentialProviderProxy_ProviderActivated(object sender, EventArgs e)
        {
            await SafePublish(new CredentialProvider_ProviderActivated(sender, e));
        }

        private async void CredentialProviderProxy_Connected(object sender, EventArgs e)
        {
            await SafePublish(new CredentialProvider_ConnectedMessage(sender, e));
        }

        private async void CredentialProviderProxy_CommandLinkPressed(object sender, EventArgs e)
        {
            await SafePublish(new CredentialProvider_CommandLinkPressedMessage(sender, e));
        }
    }
}
