﻿using GalaSoft.MvvmLight.Messaging;
using HideezSafe.Messages;
using HideezSafe.Utilities;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HideezSafe.Modules
{
    class WorkstationManager : IWorkstationManager
    {
        public WorkstationManager(IMessenger messanger)
        {
            // Start listening command messages
            messanger.Register<LockPCCommand>(this, LockPC);
            messanger.Register<ForceShutdownCommand>(this, ForceShutdown);
        }

        public void LockPC()
        {
            Win32Helper.LockWorkStation();
        }

        public void ForceShutdown()
        {
            var process = new ProcessStartInfo("shutdown", "/s /f /t 0")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(process);
        }

        #region Messages handlers

        private void LockPC(LockPCCommand command)
        {
            try
            {
                LockPC();
            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.WriteLine(ex);
            }
        }

        private void ForceShutdown(ForceShutdownCommand command)
        {
            try
            {
                ForceShutdown();
            }
            catch (Exception ex)
            {
                Debug.Assert(false);
                Debug.WriteLine(ex);
            }
        }

        #endregion Messages handlers
    }
}
