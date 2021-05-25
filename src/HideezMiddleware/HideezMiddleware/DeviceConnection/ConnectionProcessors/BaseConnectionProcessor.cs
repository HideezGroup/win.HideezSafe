using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.IPC.Messages;
using Meta.Lib.Modules.PubSub;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.ConnectionProcessors
{
    public abstract class BaseConnectionProcessor : Logger, IConnectionProcessor
    {
        readonly ConnectionFlowProcessorBase _connectionFlowProcessor;
        readonly SessionSwitchSubject _unlockMethod;
        readonly IMetaPubSub _messenger;

        public BaseConnectionProcessor(ConnectionFlowProcessorBase connectionFlowProcessor,
            SessionSwitchSubject unlockMethod,
            string logSource, 
            IMetaPubSub messenger,
            ILog log)
            : base(logSource, log)
        {
            _messenger = messenger;
            _unlockMethod = unlockMethod;
            _connectionFlowProcessor = connectionFlowProcessor ?? throw new ArgumentNullException(nameof(connectionFlowProcessor));
        }

        public abstract void Start();

        public abstract void Stop();

        protected async Task ConnectAndUnlockByConnectionId(ConnectionId connectionId)
        {
            await _connectionFlowProcessor.ConnectAndUnlock(connectionId, OnUnlockAttempt);
        }

        void OnUnlockAttempt(WorkstationUnlockResult result)
        {
            if (result.IsSuccessful)
            {
                _messenger.Publish(new WorkstationUnlockPerformedMessage(result.FlowId,
                    result.IsSuccessful,
                    _unlockMethod,
                    result.AccountName,
                    result.AccountLogin,
                    result.DeviceMac));
            }
        }
    }
}
