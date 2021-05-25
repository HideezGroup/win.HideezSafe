using Meta.Lib.Modules.PubSub;
using System;

namespace HideezMiddleware.Modules.CredentialProvider.Messages
{
    internal sealed class CredentialProvider_CommandLinkPressedMessage : PubSubMessageBase
    {
        public object Sender { get; }

        public EventArgs EventArgs { get; }

        public CredentialProvider_CommandLinkPressedMessage(object sender, EventArgs eventArgs)
        {
            Sender = sender;
            EventArgs = eventArgs;
        }
    }
}
