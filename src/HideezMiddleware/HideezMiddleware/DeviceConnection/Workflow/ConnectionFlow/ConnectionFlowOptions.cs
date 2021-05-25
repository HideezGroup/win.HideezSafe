
namespace HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow
{
    public sealed class ConnectionFlowOptions
    {
        public bool RebondOnConnectionFail { get; set; }
        public bool TryUnlock { get; set; }
        public bool UseReconnectProcedure { get; set; }

        public static ConnectionFlowOptions None
        {
            get
            {
                return new ConnectionFlowOptions();
            }
        }
    }
}
