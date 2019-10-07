﻿using Microsoft.Win32;

namespace HideezMiddleware
{
    public static class SessionSwitchMonitor
    {
        public delegate void ExtendedSessionSwitchEventHandler(int sessionId, SessionSwitchReason reason);

        public static event ExtendedSessionSwitchEventHandler SessionSwitch;

        public static void SystemSessionSwitch(int sessionId, SessionSwitchReason reason)
        {
            SessionSwitch?.Invoke(sessionId, reason);
        }
    }
}
