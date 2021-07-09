using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Log;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HideezMiddleware.Audit
{
    // Todo: Code cleanup for SessionSwitchLogger
    public class SessionSwitchLogger : Logger, IDisposable
    {
        // There is a race condition between programmatic lock/unlock by our app and actual session switch
        const int LOCK_EVENT_TIMEOUT = 3_000;

        class LockProcedure
        {
            public DateTime Time { get; set; }
            public string Mac { get; set; }
            public WorkstationLockingReason Reason { get; set; }
        }

        readonly EventSaver _eventSaver;
        readonly SessionUnlockMethodMonitor _sessionUnlockMethodMonitor;
        readonly WorkstationLockProcessor _workstationLockProcessor;
        readonly DeviceManager _deviceManager;

        LockProcedure _lockProcedure = null;
        readonly object _lpLock = new object();

        public SessionSwitchLogger(EventSaver eventSaver,
            SessionUnlockMethodMonitor sessionUnlockMethodMonitor,
            WorkstationLockProcessor workstationLockProcessor,
            DeviceManager deviceManager,
            ILog log)
            : base(nameof(SessionSwitchLogger), log)
        {
            _eventSaver = eventSaver;
            _sessionUnlockMethodMonitor = sessionUnlockMethodMonitor;
            _workstationLockProcessor = workstationLockProcessor;
            _deviceManager = deviceManager;

            _workstationLockProcessor.WorkstationLocking += WorkstationLockProcessor_WorkstationLocking;
            SessionSwitchMonitor.SessionSwitch += SessionSwitchMonitor_SessionSwitch;
        }


        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
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

        void WorkstationLockProcessor_WorkstationLocking(object sender, WorkstationLockingEventArgs e)
        {
            lock (_lpLock)
            {
                _lockProcedure = new LockProcedure()
                {
                    Time = DateTime.UtcNow,
                    Mac = e.Device.Mac,
                    Reason = e.Reason,
                };
            }
        }

       

        async void SessionSwitchMonitor_SessionSwitch(int sessionId, SessionSwitchReason reason)
        {
            bool isLock = false;
            bool isUnlock = false;

            WorkstationEventType eventType;
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
                default:
                    return; // Ignore all events except lock/unlock and logoff/logon
            }

            try
            {
                if (isLock)
                    await OnWorkstationLock(sessionId, eventType);
                else if (isUnlock)
                    await OnWorkstationUnlock(sessionId, eventType);
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        async Task OnWorkstationLock(int sessionId, WorkstationEventType eventType)
        {
            if (eventType != WorkstationEventType.ComputerLock && 
                eventType != WorkstationEventType.ComputerLogoff)
                return;

            var we = _eventSaver.GetPrevSessionWorkstationEvent();
            we.EventId = eventType;

            lock (_lpLock)
            {
                if (_lockProcedure != null && (DateTime.UtcNow - _lockProcedure.Time).TotalSeconds <= LOCK_EVENT_TIMEOUT)
                {
                    we.Note = _lockProcedure.Reason.ToString();
                    we.DeviceId = _deviceManager.Devices
                        .FirstOrDefault( d => d.Mac == _lockProcedure.Mac 
                        && d.ChannelNo == (byte)DefaultDeviceChannel.Main)?.SerialNo;
                }
                else
                    we.Note = WorkstationLockingReason.ThirdParty.ToString();

                _lockProcedure = null;
            }

            await _eventSaver.AddNewAsync(we, true);
        }

        async Task OnWorkstationUnlock(int sessionId, WorkstationEventType eventType)
        {
            if (eventType != WorkstationEventType.ComputerUnlock &&
                eventType != WorkstationEventType.ComputerLogon)
                return;

            var time = DateTime.UtcNow;
            
            var procedure = _sessionUnlockMethodMonitor.UnlockProcedure;
            if (procedure != null)
            {
                WriteLine("Wait for unlock procedure");
                await procedure.WaitFinish();
            }

            WriteLine("Generating unlock event");
            var we = _eventSaver.GetWorkstationEvent();
            we.EventId = eventType;
            we.Note = SessionSwitchSubject.Manual.ToString();
            we.Date = time;


            if (procedure != null && 
                procedure.FlowUnlockResult != null && 
                procedure.FlowUnlockResult.IsSuccessful)
            {
                var unlockMethod = await _sessionUnlockMethodMonitor.GetUnlockMethodAsync();
                we.Note = unlockMethod.ToString();
                we.DeviceId = _deviceManager.Devices
                        .FirstOrDefault(d => d.Mac == procedure.FlowUnlockResult.DeviceMac
                       && d.ChannelNo == (byte)DefaultDeviceChannel.Main)?.SerialNo;
                we.AccountLogin = procedure.FlowUnlockResult.AccountLogin;
                we.AccountName = procedure.FlowUnlockResult.AccountName;
                WriteLine($"Procedure successful ({we.DeviceId}, method: {we.Note})");
            }

            await _eventSaver.AddNewAsync(we, true);

        }
    }
}
