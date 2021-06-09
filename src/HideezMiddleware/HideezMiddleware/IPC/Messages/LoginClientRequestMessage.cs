using Meta.Lib.Modules.PubSub;
using System.Globalization;

namespace HideezMiddleware.IPC.Messages
{
    public class LoginClientRequestMessage : PubSubMessageBase
    {
        public CultureInfo ClientCulture { get; }

        public LoginClientRequestMessage(CultureInfo clientCulture)
        {
            ClientCulture = clientCulture;
        }
    }
}
