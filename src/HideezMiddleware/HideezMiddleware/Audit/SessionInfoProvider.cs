﻿using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Workstation;
using Microsoft.Win32;
using System;

namespace HideezMiddleware.Audit
{
    public class SessionInfoProvider : Logger, ISessionInfoProvider
    {
        public SessionInfo CurrentSession { get; private set; } = null;

        /// <summary>
        /// This property is used exclusivelly for lock event, because at that moment session id already changed
        /// </summary>
        public SessionInfo PreviousSession { get; private set; } = null;

        public SessionInfoProvider(ILog log)
            : base(nameof(SessionInfoProvider), log)
        {
            SessionSwitchMonitor.SessionSwitch += SessionSwitchMonitor_SessionSwitch;

            GenerateNewIdIfUnlocked();
        }

        void GenerateNewIdIfUnlocked()
        {
            var sid = WorkstationHelper.GetSessionId();
            var state = WorkstationHelper.GetSessionLockState(sid);
            WriteLine($"Startup sid:{sid}, state:{state}");
            if (state == WorkstationHelper.LockState.Unlocked)
            {
                GenerateNewSessionId();
            }
        }
        
        void SessionSwitchMonitor_SessionSwitch(int sessionId, SessionSwitchReason reason)
        {
            switch (reason)
            {
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.SessionLogoff:
                    WriteLine($"Session state changed to: locked (reason: {reason});");
                    ClearSessionId();
                    break;
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.SessionUnlock:
                    WriteLine($"Session state changed to: unlocked (reason: {reason});");
                    GenerateNewSessionId();
                    break;
            }
        }

        void GenerateNewSessionId()
        {
            PreviousSession = CurrentSession;
            var virtualSessionId = Guid.NewGuid().ToString();
            var sessionName = WorkstationHelper.GetSessionName(WorkstationHelper.GetSessionId());
            CurrentSession = new SessionInfo(virtualSessionId, sessionName);
            WriteLine($"Generated new session id: (current: {CurrentSession?.SessionId}), (prev: {PreviousSession?.SessionId})");
        }

        void ClearSessionId()
        {
            PreviousSession = CurrentSession;
            CurrentSession = null;
            WriteLine($"Cleared session id: (current: {CurrentSession?.SessionId}), (prev: {PreviousSession?.SessionId})");
        }
    }
}