﻿using HideezServiceHost.HideezServiceReference;

namespace HideezServiceHost
{
    class HideezServiceCallbacks : IHideezServiceCallback
    {
        // All callbacks in HideezServiceHost can be left empty / not implemented
        // The service host connects to the service briefly to initialize the primary library
        // After initialization the connection is closed

        // If new callback is added to interface, create empty implementation without any logic

        public void LockWorkstationRequest()
        {
        }
    }
}
