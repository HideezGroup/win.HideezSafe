﻿using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.WorkstationEvents;
using HideezMiddleware;
using HideezMiddleware.DeviceConnection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceLibrary.Implementation.AuditLogs
{
    // Todo: Code cleanup for SessionSwitchLogger
    class SessionSwitchLogger : Logger, IDisposable
    {
        // There is a delay between successful unlock/logon by application and actual session switch
        const int LOCK_EVENT_LIFETIME = 30;
        const int UNLOCK_EVENT_LIFETIME = 120;

        class HideezLockEvent
        {
            public DateTime EventTime { get; set; }

            public string Mac { get; set; }

            public WorkstationLockingReason Reason { get; set; }
        }

        class HideezUnlockEvent
        {
            public DateTime EventTime { get; set; }

            public string Mac { get; set; }

            public SessionSwitchSubject Reason { get; set; }
        }

        readonly EventAggregator _eventAggregator;
        readonly TapConnectionProcessor _tapProcessor;
        readonly RfidConnectionProcessor _rfidProcessor;
        readonly ProximityConnectionProcessor _proximityProcessor;
        readonly WorkstationLockProcessor _workstationLockProcessor;
        readonly BleDeviceManager _bleDeviceManager;

        readonly Dictionary<string, HideezLockEvent> lockEventsList = new Dictionary<string, HideezLockEvent>();
        readonly Dictionary<string, HideezUnlockEvent> unlockEventsList = new Dictionary<string, HideezUnlockEvent>();

        readonly object listsLock = new object();

        public SessionSwitchLogger(EventAggregator eventAggregator,
            TapConnectionProcessor tapProcessor, 
            RfidConnectionProcessor rfidProcessor,
            ProximityConnectionProcessor proximityProcessor,
            WorkstationLockProcessor workstationLockProcessor,
            BleDeviceManager bleDeviceManager,
            ILog log)
            : base(nameof(SessionSwitchLogger), log)
        {
            _eventAggregator = eventAggregator;
            _tapProcessor = tapProcessor;
            _rfidProcessor = rfidProcessor;
            _proximityProcessor = proximityProcessor;
            _workstationLockProcessor = workstationLockProcessor;
            _bleDeviceManager = bleDeviceManager;

            tapProcessor.WorkstationUnlockPerformed += TapProcessor_WorkstationUnlockPerformed;
            rfidProcessor.WorkstationUnlockPerformed += RfidProcessor_WorkstationUnlockPerformed;
            proximityProcessor.WorkstationUnlockPerformed += ProximityProcessor_WorkstationUnlockPerformed;

            workstationLockProcessor.WorkstationLocking += WorkstationLockProcessor_WorkstationLocking;

            SessionSwitchMonitor.SessionSwitch += SessionSwitchMonitor_SessionSwitch;
        }

        void WorkstationLockProcessor_WorkstationLocking(object sender, WorkstationLockingEventArgs e)
        {
            lock (listsLock)
            {
                lockEventsList.Add(e.Device.Mac, new HideezLockEvent()
                {
                    EventTime = DateTime.UtcNow,
                    Mac = e.Device.Mac,
                    Reason = e.Reason,
                });
            }
        }

        void TapProcessor_WorkstationUnlockPerformed(object sender, string e)
        {
            lock (listsLock)
            {
                unlockEventsList.Add(e, new HideezUnlockEvent()
                {
                    EventTime = DateTime.UtcNow,
                    Mac = e,
                    Reason = SessionSwitchSubject.Dongle,
                });
            }
        }

        void RfidProcessor_WorkstationUnlockPerformed(object sender, string e)
        {
            lock (listsLock)
            {
                unlockEventsList.Add(e, new HideezUnlockEvent()
                {
                    EventTime = DateTime.UtcNow,
                    Mac = e,
                    Reason = SessionSwitchSubject.RFID,
                });
            }
        }

        void ProximityProcessor_WorkstationUnlockPerformed(object sender, string e)
        {
            lock (listsLock)
            {
                unlockEventsList.Add(e, new HideezUnlockEvent()
                {
                    EventTime = DateTime.UtcNow,
                    Mac = e,
                    Reason = SessionSwitchSubject.Proximity,
                });
            }
        }

        async void SessionSwitchMonitor_SessionSwitch(int sessionId, SessionSwitchReason reason)
        {
            if (!reason.HasFlag(SessionSwitchReason.SessionLock | SessionSwitchReason.SessionLogoff 
                | SessionSwitchReason.SessionLogon | SessionSwitchReason.SessionUnlock))
                return;

            WorkstationEventType eventType = 0;

            bool isLock = false;
            bool isUnlock = false;

            switch (reason)
            {
                case SessionSwitchReason.SessionLock:
                    eventType = WorkstationEventType.ComputerLock;
                    isLock = true;
                    break;
                case SessionSwitchReason.SessionLogoff:
                    eventType = WorkstationEventType.ComputerLogoff;
                    isLock = true;
                    break;
                case SessionSwitchReason.SessionUnlock:
                    eventType = WorkstationEventType.ComputerUnlock;
                    isUnlock = true;
                    break;
                case SessionSwitchReason.SessionLogon:
                    eventType = WorkstationEventType.ComputerLogon;
                    isUnlock = true;
                    break;
            }

            try
            {
                if (isLock)
                    await RecordWorkstationLock(sessionId, eventType);
                else if (isUnlock)
                    await RecordWorkstationUnlock(sessionId, eventType);
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed = false;
        void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    _tapProcessor.WorkstationUnlockPerformed -= TapProcessor_WorkstationUnlockPerformed;
                    _rfidProcessor.WorkstationUnlockPerformed -= RfidProcessor_WorkstationUnlockPerformed;
                    _proximityProcessor.WorkstationUnlockPerformed -= ProximityProcessor_WorkstationUnlockPerformed;
                    _workstationLockProcessor.WorkstationLocking -= WorkstationLockProcessor_WorkstationLocking;
                    SessionSwitchMonitor.SessionSwitch -= SessionSwitchMonitor_SessionSwitch;
                }

                disposed = true;
            }
        }

        ~SessionSwitchLogger()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }
        #endregion

        async Task RecordWorkstationLock(int sessionId, WorkstationEventType eventType)
        {
            var we = EventFactory.GetWorkstationEvent();
            we.EventId = eventType;

            lock (listsLock)
            {
                var now = DateTime.UtcNow;
                var latestLockEvent = lockEventsList.Values
                    .Where(e => (now - e.EventTime).TotalSeconds < LOCK_EVENT_LIFETIME)
                    .OrderByDescending(e => e.EventTime)
                    .FirstOrDefault();

                if (latestLockEvent != null)
                {
                    we.Note = latestLockEvent.Reason.ToString();
                    we.DeviceId = _bleDeviceManager.Find(latestLockEvent.Mac, 1)?.SerialNo;
                }

                lockEventsList.Clear();
            }

            await _eventAggregator?.AddNewAsync(we, true);
        }

        async Task RecordWorkstationUnlock(int sessionId, WorkstationEventType eventType)
        {
            var we = EventFactory.GetWorkstationEvent();
            we.EventId = eventType;

            lock (listsLock)
            {
                var now = DateTime.UtcNow;

                var latestUnlockEvent = unlockEventsList.Values
                    .Where(e => (now - e.EventTime).TotalSeconds < UNLOCK_EVENT_LIFETIME)
                    .OrderByDescending(e => e.EventTime)
                    .FirstOrDefault();

                if (latestUnlockEvent != null)
                {
                    we.Note = latestUnlockEvent.Reason.ToString();
                    we.DeviceId = _bleDeviceManager.Find(latestUnlockEvent.Mac, 1)?.SerialNo;
                }

                unlockEventsList.Clear();
            }

            await _eventAggregator?.AddNewAsync(we, true);
        }
    }
}
