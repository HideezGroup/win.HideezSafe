using ButtonPressTrackingSample;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Device.Exchange;
using HideezMiddleware.IPC.DTO;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.IPC.Messages;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using Meta.Lib.Utils;
using Nito.AsyncEx;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ButtonPressTrackingSample
{
    class Program
    {
        static IMetaPubSub _metaMessenger = new MetaPubSub();
        static TaskCompletionSource<DeviceDTO> _connectionTcs = new TaskCompletionSource<DeviceDTO>();
        static TaskCompletionSource<bool> _buttonPressTcs = new TaskCompletionSource<bool>();

        static void Main(string[] args)
        {
            AsyncContext.Run(() => MainAsync(args));
        }

        static async void MainAsync(string[] args)
        {
            try
            {
                await _metaMessenger.TrySubscribeOnServer<DevicesCollectionChangedMessage>(OnDevicesCollectionChanged);
                await _metaMessenger.TrySubscribeOnServer<DeviceConnectionStateChangedMessage>(OnDeviceConnectionStateChanged);
                _metaMessenger.Subscribe<ConnectedToServerEvent>(OnConnectedToServer);
                await _metaMessenger.TryConnectToServer("HideezServicePipe");

                var connectedDevice = await WaitConnection();
                if (connectedDevice != null)
                {
                    Console.WriteLine($"Connected device is {connectedDevice.Id}");

                    var remoteDevice = await CreateRemoteDevice(connectedDevice);
                    //await remoteDevice.VerifyAndInitialize();
                    //await remoteDevice.RefreshDeviceInfo(); //pull metadata from service(SerialNo, Firware, Battery etc)

                    remoteDevice.ButtonPressed += RemoteDevice_ButtonPressed;

                    var pressedBtn = await WaitButtonPress();
                    if (!pressedBtn)
                        Console.WriteLine("No button was detected");
                }
                else
                    Console.WriteLine("No connected devices");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("Press any key...");
            Console.ReadLine();
        }

        static async Task<DeviceDTO> WaitConnection()
        {
            Console.WriteLine("Waiting connection...");
            try
            {
                return await _connectionTcs.Task.TimeoutAfter(30000);
            }
            catch
            {
                return null;
            }

        }

        static async Task<bool> WaitButtonPress()
        {
            Console.WriteLine("Waiting for button press..");
            try
            {
                return await _buttonPressTcs.Task.TimeoutAfter(60000);
            }
            catch
            {
                return false;
            }
        }

        static Task OnDevicesCollectionChanged(DevicesCollectionChangedMessage message)
        {
            foreach (var deviceDto in message.Devices)
            {
                if (deviceDto.IsConnected)
                    _connectionTcs.TrySetResult(deviceDto);
            }

            return Task.CompletedTask;
        }

        static Task OnDeviceConnectionStateChanged(DeviceConnectionStateChangedMessage message)
        {
            Console.WriteLine($"Device {message.Device.Mac} connection state changed: IsConnected - {message.Device.IsConnected}");
            if (message.Device.IsConnected)
                _connectionTcs.TrySetResult(message.Device);
            return Task.CompletedTask;
        }

        //todo: only be used by client applications
        async static Task OnConnectedToServer(ConnectedToServerEvent arg)
        {
            await _metaMessenger.PublishOnServer(new RefreshServiceInfoMessage());
        }

        async static Task<Device> CreateRemoteDevice(DeviceDTO deviceDTO)
        {
            var channelsReply = await _metaMessenger.ProcessOnServer<GetAvailableChannelsMessageReply>(new GetAvailableChannelsMessage(deviceDTO.SerialNo));
            var channels = channelsReply.FreeChannels;
            if (channels.Length == 0)
                throw new Exception("No available channels");
            var channelNo = channels.FirstOrDefault();

            var reply = await _metaMessenger.ProcessOnServer<EstablishRemoteDeviceConnectionMessageReply>(new EstablishRemoteDeviceConnectionMessage(deviceDTO.NotificationsId, channelNo));
            var remoteDeviceId = reply.RemoteDeviceId;

            var remoteDeviceMessenger = new MetaPubSub();
            await remoteDeviceMessenger.TryConnectToServer(reply.PipeName); //Establish remote connection with device in service

            var pipeRemoteDeviceConnection = new PipeRemoteDeviceConnection(remoteDeviceMessenger, remoteDeviceId, reply.DeviceName, reply.DeviceMac);
            var commandQueue = new CommandQueue(pipeRemoteDeviceConnection, null);
            var deviceCommands = new RemoteDeviceCommands(remoteDeviceMessenger, remoteDeviceId);
            var device = new Device(commandQueue, channelNo, deviceCommands, null);

            return device;
        }

        static void RemoteDevice_ButtonPressed(object sender, ButtonPressCode e)
        {
            Console.WriteLine($"Vault button pressed, code: {e}");
            _buttonPressTcs.TrySetResult(true);
        }
    }
}
