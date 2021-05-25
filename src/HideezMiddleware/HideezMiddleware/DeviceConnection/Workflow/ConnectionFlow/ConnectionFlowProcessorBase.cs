using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.Modules.ServiceEvents.Messages;
using Meta.Lib.Modules.PubSub;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow
{
    public abstract class ConnectionFlowProcessorBase : Logger
    {
        protected IMetaPubSub _messenger;

        int _workflowInterlock = 0;
        CancellationTokenSource _cts;

        public abstract event EventHandler<string> Started;
        public abstract event EventHandler<string> AttemptingUnlock;
        public abstract event EventHandler<string> UnlockAttempted;
        public abstract event EventHandler<IDevice> DeviceFinilizingMainFlow;
        public abstract event EventHandler<IDevice> DeviceFinishedMainFlow;
        public abstract event EventHandler<string> Finished;

        public bool IsRunning { get; protected set; }

        public ConnectionFlowProcessorBase(IMetaPubSub messenger, string name, ILog log) : base(name, log)
        {
            _messenger = messenger;

            _messenger.Subscribe<SessionSwitchMonitor_SessionSwitchMessage>(OnSessionSwitch);
            _messenger.Subscribe<PowerEventMonitor_SystemQuerySuspendMessage>(OnSystemQuerySuspend);
            _messenger.Subscribe<PowerEventMonitor_SystemSuspendingMessage>(OnSystemSuspending);
            _messenger.Subscribe<PowerEventMonitor_SystemLeftSuspendedModeMessage>(OnSystemLeavingSuspend);
        }

        private Task OnSessionSwitch(SessionSwitchMonitor_SessionSwitchMessage arg)
        {
            // Cancel the workflow if session switches to an unlocked (or different one)
            // Keep in mind, that workflow can cancel itself due to successful workstation unlock
            Cancel("Session switched");

            return Task.CompletedTask;
        }

        private Task OnSystemQuerySuspend(PowerEventMonitor_SystemQuerySuspendMessage arg)
        {
            // Cancel the workflow if system is preparing to suspending
            Cancel("System preparing to suspend");

            return Task.CompletedTask;
        }

        private Task OnSystemSuspending(PowerEventMonitor_SystemSuspendingMessage arg)
        {
            // Cancel the workflow if system is suspending
            Cancel("System suspending");

            return Task.CompletedTask;
        }

        private Task OnSystemLeavingSuspend(PowerEventMonitor_SystemLeftSuspendedModeMessage arg)
        {
            // Cancel the workflow if, for some reason, there is one 
            // when system is leaving suspended mode
            Cancel("System left suspended mode");

            return Task.CompletedTask;
        }

        protected void OnVaultDisconnectedDuringFlow(object sender, EventArgs e)
        {
            // Cancel the workflow if the vault disconnects
            Cancel("Vault unexpectedly disconnected");
        }

        protected void OnCancelledByVaultButton(object sender, EventArgs e)
        {
            // Cancel the workflow if the user pressed the cancel button (long button press)
            Cancel("User pressed the cancel button");
        }

        public void Cancel(string reason)
        {
            if (_cts != null)
            {
                WriteLine($"Canceling; {reason}");
                _cts?.Cancel();
            }
        }

        public async Task Connect(ConnectionId connectionId)
        {
            // ignore, if already performing workflow for any device
            if (Interlocked.CompareExchange(ref _workflowInterlock, 1, 0) == 0)
            {
                try
                {
                    _cts = new CancellationTokenSource();
                    await MainWorkflow(connectionId, ConnectionFlowOptions.None, null, _cts.Token);
                }
                finally
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;

                    Interlocked.Exchange(ref _workflowInterlock, 0);
                }
            }

        }

        public async Task Reconnect(ConnectionId connectionId)
        {
            // ignore, if already performing workflow for any device
            if (Interlocked.CompareExchange(ref _workflowInterlock, 1, 0) == 0)
            {
                try
                {
                    _cts = new CancellationTokenSource();
                    var workflowOptions = new ConnectionFlowOptions
                    {
                        UseReconnectProcedure = true,
                    };
                    await MainWorkflow(connectionId, workflowOptions, null, _cts.Token);
                }
                finally
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;

                    Interlocked.Exchange(ref _workflowInterlock, 0);
                }
            }
        }

        public async Task ConnectAndUnlock(ConnectionId connectionId, Action<WorkstationUnlockResult> onSuccessfulUnlock)
        {
            // ignore, if already performing workflow for any device
            if (Interlocked.CompareExchange(ref _workflowInterlock, 1, 0) == 0)
            {
                try
                {
                    _cts = new CancellationTokenSource();
                    var workflowOptions = new ConnectionFlowOptions
                    {
                        RebondOnConnectionFail = connectionId.IdProvider == (byte)DefaultConnectionIdProvider.Csr,
                        TryUnlock = true,
                    };
                    await MainWorkflow(connectionId, workflowOptions, onSuccessfulUnlock, _cts.Token);
                }
                finally
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;

                    Interlocked.Exchange(ref _workflowInterlock, 0);
                }
            }
        }

        protected abstract Task MainWorkflow(ConnectionId connectionId, ConnectionFlowOptions options, Action<WorkstationUnlockResult> onUnlockAttempt, CancellationToken ct);
    }
}
