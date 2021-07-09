using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.Tasks;
using HideezMiddleware.Utils.WorkstationHelper;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace HideezMiddleware
{
    public class SessionUnlockMethodMonitor : Logger
    {
        readonly ConnectionFlowProcessorBase _connectionFlowProcessor;
        readonly IWorkstationHelper _workstationHelper;
        readonly IMetaPubSub _messenger;

        UnlockSessionSwitchProc _unlockProcedure = null;

        internal UnlockSessionSwitchProc UnlockProcedure { get => _unlockProcedure; }

        public SessionUnlockMethodMonitor(ConnectionFlowProcessorBase connectionFlowProcessor,
            IWorkstationHelper workstationHelper,
            IMetaPubSub messenger,
            ILog log)
            : base(nameof(SessionUnlockMethodMonitor), log)
        {
            _connectionFlowProcessor = connectionFlowProcessor;
            _workstationHelper = workstationHelper;
            _messenger = messenger;

            _connectionFlowProcessor.AttemptingUnlock += ConnectionFlowProcessor_AttemptingUnlock;
        }

        void ConnectionFlowProcessor_AttemptingUnlock(object sender, string flowId)
        {
            if (_workstationHelper.IsCurrentSessionLocked())
            {
                Task.Run(() =>
                {
                    _unlockProcedure = new UnlockSessionSwitchProc(flowId, _connectionFlowProcessor, _messenger);
                    Task.Run(_unlockProcedure.Run);
                    WriteLine("Started unlock procedure");
                });
            }
        }

        void SessionSwitchMonitor_SessionSwitch(int sessionId, SessionSwitchReason reason)
        {
            if (reason == SessionSwitchReason.SessionLogoff || reason == SessionSwitchReason.SessionLock)
                _unlockProcedure = null;
        }

        public async Task<SessionSwitchSubject> GetUnlockMethodAsync()
        {
            if (_unlockProcedure != null)
            {
                await _unlockProcedure.WaitFinish(); 

                if (_unlockProcedure.FlowFinished && _unlockProcedure.FlowUnlockResult.IsSuccessful)
                    return _unlockProcedure.UnlockMethod;
            }

            return SessionSwitchSubject.Manual;
        }
    }
}
