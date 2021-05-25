using Meta.Lib.Modules.PubSub;
using System;

namespace HideezMiddleware.Modules.CredentialProvider.Messages
{
    internal sealed class CredentialProvider_ConnectedMessage : PubSubMessageBase
    {
        public object Sender { get; }
        public EventArgs Args { get; }

        public CredentialProvider_ConnectedMessage(object sender, EventArgs args)
        {
            Sender = sender;
            Args = args;
        }
    }
}
