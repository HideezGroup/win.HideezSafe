using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Utils;
using HideezMiddleware.DeviceConnection;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.IPC.Messages;
using Meta.Lib.Modules.PubSub;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.Tasks
{
    internal sealed class UnlockSessionSwitchProc
    {
        readonly ConnectionFlowProcessorBase _connectionFlowProcessor;
        readonly IMetaPubSub _messenger;

        readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();
        readonly string _flowId;

        readonly int _timeout;

        public WorkstationUnlockResult FlowUnlockResult { get; private set; } = null; // Set on connectionFlow.UnlockAttempt
        public SessionSwitchSubject UnlockMethod { get; private set; } = SessionSwitchSubject.Manual; // Changed on connectionFlow.UnlockAttempt
        public bool FlowFinished { get; private set; } // Set on connectionFlow.Finished

        public DateTime SessionSwitchEventTime { get; private set; } // Set on SessionSwitchMonitor.SessionSwitch
        public bool SessionSwitched { get; private set; } // Set on SessionSwitchMonitor.SessionSwitch

        public UnlockSessionSwitchProc(
            string flowId,
            ConnectionFlowProcessorBase connectionFlowProcessor,
            IMetaPubSub messenger,
            int timeout = 10_000)
        {
            _flowId = flowId;
            _connectionFlowProcessor = connectionFlowProcessor;
            _messenger = messenger;

            _timeout = timeout;
        }

        public async Task Run()
        {
            try
            {
                SubscribeToEvents();
                
                // Cancel if unlock failed or flow finished
                await _tcs.Task.TimeoutAfter(_timeout);
            }
            catch (TimeoutException)
            {
                _tcs.TrySetResult(new object());
            }
            finally
            {
                UnsubscribeFromEvents();
            }
        }

        public async Task WaitFinish()
        {
            try
            {
                await _tcs.Task.TimeoutAfter(_timeout);
            }
            catch (TimeoutException)
            {
            }
        }

        void SubscribeToEvents()
        {
            _connectionFlowProcessor.Finished += ConnectionFlowProcessor_Finished;
            _messenger.Subscribe<WorkstationUnlockPerformedMessage>(OnWorkstationUnlockPerformed);
        }

        void UnsubscribeFromEvents()
        {
            _connectionFlowProcessor.Finished -= ConnectionFlowProcessor_Finished;
            _messenger.Unsubscribe<WorkstationUnlockPerformedMessage>(OnWorkstationUnlockPerformed);
        }

        void ConnectionFlowProcessor_Finished(object sender, string e)
        {
            if (e == _flowId)
            {
                FlowFinished = true;

                _tcs.TrySetResult(new object());
            }
        }

        private Task OnWorkstationUnlockPerformed(WorkstationUnlockPerformedMessage msg)
        {
            if (msg.FlowId == _flowId)
            {
                FlowUnlockResult = new WorkstationUnlockResult
                {
                    FlowId = msg.FlowId,
                    IsSuccessful = msg.IsSuccessful,
                    DeviceMac = msg.Mac,
                    AccountName = msg.AccountName,
                    AccountLogin = msg.AccountLogin,
                }; 
                UnlockMethod = msg.UnlockMethod;
            }

            return Task.CompletedTask;
        }
    }
}
