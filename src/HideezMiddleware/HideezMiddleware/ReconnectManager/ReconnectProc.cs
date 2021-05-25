using Hideez.SDK.Communication.Interfaces;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.ReconnectManager
{
    class ReconnectProc
    {
        readonly IDevice _device;
        readonly ConnectionFlowProcessorBase _connectionFlowProcessor;
        bool _isReconnectSuccessful = false;

        public ReconnectProc(IDevice device, ConnectionFlowProcessorBase connectionFlowProcessor)
        {
            _device = device;
            _connectionFlowProcessor = connectionFlowProcessor;
        }

        public async Task<bool> Run()
        {
            try
            {
                _connectionFlowProcessor.DeviceFinilizingMainFlow += ConnectionFlowProcessor_DeviceFinilizingMainFlow;
                await _connectionFlowProcessor.Reconnect(_device.DeviceConnection.Connection.ConnectionId);
                return _isReconnectSuccessful;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _connectionFlowProcessor.DeviceFinishedMainFlow -= ConnectionFlowProcessor_DeviceFinilizingMainFlow;
            }

        }

        void ConnectionFlowProcessor_DeviceFinilizingMainFlow(object sender, IDevice e)
        {
            if (e.Id == _device.Id)
                _isReconnectSuccessful = true;
        }
    }
}
