﻿namespace HideezSafe.Messages
{
    class ConnectionHESChangedMessage : ConnectionChangedMessage
    {
        public ConnectionHESChangedMessage(bool isConnected)
            : base(isConnected)
        {
        }
    }
}
