using Meta.Lib.Modules.PubSub;
using System;

namespace HideezMiddleware.Modules.CredentialProvider.Messages
{
    internal sealed class CredentialProvider_ProviderActivated : PubSubMessageBase
    {
        public object Sender { get; }
        public EventArgs Args { get; }

        public CredentialProvider_ProviderActivated(object sender, EventArgs args)
        {
            Sender = sender;
            Args = args;
        }
    }
}
