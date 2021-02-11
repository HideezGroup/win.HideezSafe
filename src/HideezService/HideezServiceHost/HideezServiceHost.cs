﻿using System;
using System.Diagnostics;
using System.ServiceProcess;
using HideezMiddleware;
using System.Threading.Tasks;
using Microsoft.Win32;
using ServiceLibrary.Implementation;

namespace HideezServiceHost
{
    public partial class HideezServiceHost : ServiceBase
    {
        HideezService _service;

        public HideezServiceHost()
        {
            CanHandlePowerEvent = true;
            CanHandleSessionChangeEvent = true;

            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                _service = new HideezServiceFactory().GetHideezService();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw;
            }
        }

        protected override void OnStop()
        {
            _service.Shutdown();
        }

        // https://stackoverflow.com/questions/44980/programmatically-determine-a-duration-of-a-locked-workstation
        protected override void OnSessionChange(SessionChangeDescription sessionChangeDescription)
        {
            SessionSwitchMonitor.SystemSessionSwitch(sessionChangeDescription.SessionId, (SessionSwitchReason)sessionChangeDescription.Reason);
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            HandlePowerEvent(powerStatus);
            return base.OnPowerEvent(powerStatus);
        }


        /*
         * https://stackoverflow.com/questions/30433432/difference-between-resumeautomatic-resumesuspend-modes-of-windows-service
         * 
         * How it's supposed to work
         * (This is not how it all works in practice - see below.)
         * 
         * ResumeAutomatic
         * This message is always sent when the computer has resumed after sleep.
         * 
         * ResumeSuspend
         * The computer has resumed after sleep, and Windows believes a user is present - i.e. that there is a human sitting in front of the machine. This message is sent when either 
         * a) the wake was caused by human interaction (someone pressing the power button,pressingakey,movingthemouse,etc); or b)thefirsttime thereishuman interaction after the machine wakes automatically due to a wake timer.
         * 
         * To summarise:
         * ResumeAutomatic is always sent when the computer resumes from sleep.
         * ResumeSuspend is sent as well as ResumeAutomatic when the computer resumes from sleep and Windows believes a user is present.
         * 
         * How it actually works
         * ResumeAutomatic occasionally isn't sent at all. This is a long-standing bug, presumably in Windows itself. Fortunately I've never seen the computer wake with both ResumeAutomatic and ResumeSuspend unsent. If you need to know that the system has resumed, but don't care whether a user's there or not, you need to listen for both ResumeAutomatic and ResumeSuspend and treat them as the same thing. 
         * 
         * ResumeSuspend is extremely unreliable. I've never seen it not sent when it's supposed to be, but it's often sent when it isn't supposed to be - when actually there's no user there at all. Whether this is due to one or more bugs in Windows, third party drivers, firmware or hardware, I have no idea. 
         * 
         * When ResumeAutomatic is sent with no corresponding ResumeSuspend the system idle timeout is brief (2 minutes by default in Windows 10) and attached displays are kept in power saving mode. When a corresponding ResumeSuspend is sent the system idle timeout is normal (30 minutes by default in Windows 10) and attached displays are woken up. This is so that the computer goes back to sleep as soon as possible if it wakes automatically to perform maintenance, etc. It would be fantastic if Microsoft could make it work reliably.
         * 
         */
        void HandlePowerEvent(PowerBroadcastStatus powerStatus)
        {
            switch (powerStatus)
            {
                case PowerBroadcastStatus.BatteryLow:
                    break;
                case PowerBroadcastStatus.OemEvent:
                    break;
                case PowerBroadcastStatus.PowerStatusChange:
                    break;
                case PowerBroadcastStatus.QuerySuspend: // System is trying to schedule suspend
                    PowerEventMonitor.InvokeSystemQuerySuspendEvent();
                    break;
                case PowerBroadcastStatus.QuerySuspendFailed: // Some application canceled suspend
                    break;
                case PowerBroadcastStatus.ResumeAutomatic: // Sleep or hibernation ended, brief system timeout (2m)
                case PowerBroadcastStatus.ResumeCritical: // Suspension because of low battery charge ended
                case PowerBroadcastStatus.ResumeSuspend: // Sleep or hibernation ended, normal system timeout (30m)
                    PowerEventMonitor.InvokeSystemLeftSuspendedModeEvent();
                    break;
                case PowerBroadcastStatus.Suspend: // System is about to be suspended, approximately 2 seconds before it happens
                    PowerEventMonitor.InvokeSystemSuspendingEvent();
                    break;
                default:
                    break;
            }
        }
    }
}
