using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow
{
    public interface IConnectionFlowProcessor
    {
        bool IsRunning { get; }

        event EventHandler<string> AttemptingUnlock;
        event EventHandler<IDevice> DeviceFinilizingMainFlow;
        event EventHandler<IDevice> DeviceFinishedMainFlow;
        event EventHandler<string> Finished;
        event EventHandler<string> Started;
        event EventHandler<string> UnlockAttempted;

        void Cancel(string reason);
        Task Connect(ConnectionId connectionId);
        Task ConnectAndUnlock(ConnectionId connectionId, Action<WorkstationUnlockResult> onSuccessfulUnlock);
        Task Reconnect(ConnectionId connectionId);
    }
}