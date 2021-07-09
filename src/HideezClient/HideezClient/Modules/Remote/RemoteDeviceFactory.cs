using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Device.Exchange;
using Hideez.SDK.Communication.Log;
using HideezClient.Modules.Log;
using HideezClient.Modules.Remote;
using HideezMiddleware.IPC.IncommingMessages;
using Meta.Lib.Modules.PubSub;
using System.Threading.Tasks;

namespace HideezClient.Modules
{
    class RemoteDeviceFactory : Logger, IRemoteDeviceFactory
    {
        readonly Logger _logger = LogManager.GetCurrentClassLogger(nameof(RemoteDeviceFactory));
        readonly IMetaPubSub _metaMessenger;

        public RemoteDeviceFactory(IMetaPubSub metaMessenger, ILog log)
            :base(nameof(RemoteDeviceFactory), log)
        {
            _metaMessenger = metaMessenger;
        }

        public async Task<Device> CreateRemoteDeviceAsync(string connectionId, byte channelNo, IMetaPubSub remoteDeviceMessenger)
        {
            _logger.WriteLine($"({connectionId}) Creating remote vault on channel:{channelNo}");
            var reply = await _metaMessenger.ProcessOnServer<EstablishRemoteDeviceConnectionMessageReply>(new EstablishRemoteDeviceConnectionMessage(connectionId, channelNo));
            var remoteDeviceId = reply.RemoteDeviceId;

            await remoteDeviceMessenger.TryConnectToServer(reply.PipeName);
            var pipeRemoteDeviceConnection = new PipeRemoteDeviceConnection(remoteDeviceMessenger, remoteDeviceId, reply.DeviceName, reply.DeviceMac);
            var commandQueue = new CommandQueue(pipeRemoteDeviceConnection, _log);
            var deviceCommands = new RemoteDeviceCommands(remoteDeviceMessenger, remoteDeviceId);
            var device = new Device(commandQueue, channelNo, deviceCommands, _log);

            _logger.WriteLine($"({connectionId}) Created remote vault with id: ({device.Id})");

            return device;
        }
    }
}
