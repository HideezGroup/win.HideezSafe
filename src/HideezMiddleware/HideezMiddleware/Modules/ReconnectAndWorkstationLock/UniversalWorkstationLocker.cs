using Hideez.SDK.Communication.Log;
using HideezMiddleware.Utils.WorkstationHelper;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.ReconnectAndWorkstationLock
{
    internal sealed class UniversalWorkstationLocker : Logger, IWorkstationLocker
    {
        class EnsureWorkstationLockProc : IDisposable
        {
            readonly int _lockTimeout;
            readonly WcfWorkstationLocker _wcfLocker;
            readonly WtsapiWorkstationLocker _wtsapiLocker;
            readonly IWorkstationHelper _workstationHelper;

            TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();

            public EnsureWorkstationLockProc(int lockTimeout, WcfWorkstationLocker wcfLocker, WtsapiWorkstationLocker wtsapiLocker, IWorkstationHelper workstationHelper)
            {
                _lockTimeout = lockTimeout;
                _wcfLocker = wcfLocker;
                _wtsapiLocker = wtsapiLocker;
                _workstationHelper = workstationHelper;
            }

            public async Task Run()
            {
#if !DEBUG
                if (_workstationHelper.GetActiveSessionLockState() == WorkstationInformationHelper.LockState.Unlocked)
                {
                    try
                    {
                        SessionSwitchMonitor.SessionSwitch += SessionSwitchMonitor_SessionSwitch;
                
                        _wcfLocker.LockWorkstation();
                
                        await Task.WhenAny(_tcs.Task, Task.Delay(_lockTimeout));
                    }
                    finally
                    {
                        SessionSwitchMonitor.SessionSwitch -= SessionSwitchMonitor_SessionSwitch;
                    }
                
                    if (!_tcs.Task.IsCompleted &&
                        _workstationHelper.GetActiveSessionLockState() == WorkstationInformationHelper.LockState.Unlocked)
                        _wtsapiLocker.LockWorkstation();
                }
#endif
            }

            void SessionSwitchMonitor_SessionSwitch(int sessionId, SessionSwitchReason reason)
            {
                if (reason == SessionSwitchReason.SessionLock ||
                    reason == SessionSwitchReason.SessionLogoff ||
                    reason == SessionSwitchReason.SessionLogon ||
                    reason == SessionSwitchReason.SessionUnlock)
                {
                    _tcs.SetResult(new object());
                }
            }

            #region IDisposable Support
            bool disposed = false; // To detect redundant calls
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                    }

                    disposed = true;
                }
            }

            ~EnsureWorkstationLockProc()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(false);
            }
            #endregion
        }

        readonly int _lockTimeout;
        readonly WcfWorkstationLocker _wcfLocker;
        readonly WtsapiWorkstationLocker _wtsapiLocker;
        readonly IWorkstationHelper _workstationHelper;

        public UniversalWorkstationLocker(int lockTimeout, IMetaPubSub messenger, IWorkstationHelper workstationHelper, ILog log)
            : base(nameof(UniversalWorkstationLocker), log)
        {
            _lockTimeout = lockTimeout;
            _wcfLocker = new WcfWorkstationLocker(messenger, workstationHelper, log);
            _wtsapiLocker = new WtsapiWorkstationLocker(workstationHelper, log);
            _workstationHelper = workstationHelper;
        }

        public void LockWorkstation()
        {
            EnsureWorkstationLockAsync();
        }

        void EnsureWorkstationLockAsync()
        {
            Task.Run(async () =>
            {
                using (var proc = new EnsureWorkstationLockProc(_lockTimeout, _wcfLocker, _wtsapiLocker, _workstationHelper))
                {
                    await proc.Run();
                }
            });
        }
    }
}
