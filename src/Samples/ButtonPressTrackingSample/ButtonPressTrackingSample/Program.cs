using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using Nito.AsyncEx;
using System;
using System.Threading.Tasks;
using Hideez.Integration.Lite.DTO;
using Hideez.Integration.Lite.Enums;
using Hideez.Integration.Lite.Messages;

namespace ButtonPressTrackingSample
{
    class Program
    {
        static IMetaPubSub _metaMessenger = new MetaPubSub();
        static void Main(string[] args)
        {
            AsyncContext.Run(() => MainAsync(args));
        }

        static async void MainAsync(string[] args)
        {
            try
            {
                _metaMessenger.Subscribe<ConnectedToServerEvent>(OnConnectedToServer);
                await _metaMessenger.TrySubscribeOnServer<DevicesCollectionChangedMessage>(OnDevicesCollectionChanged);
                await _metaMessenger.TrySubscribeOnServer<DeviceConnectionStateChangedMessage>(OnDeviceConnectionStateChanged);
                await _metaMessenger.TrySubscribeOnServer<DeviceStateChangedMessage>(OnDeviceStateChanged);
                await _metaMessenger.TryConnectToServer("HideezServicePipe");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("Press 'Enter' to exit");
            Console.ReadLine();
        }

        private static Task OnDeviceStateChanged(DeviceStateChangedMessage arg)
        {
            if (arg.Button != ButtonPressCode.None)
                Console.WriteLine($"Vault {arg.DeviceId} button pressed, code: {arg.Button}");
            return Task.CompletedTask;
        }

        static Task OnDevicesCollectionChanged(DevicesCollectionChangedMessage message)
        {
            foreach (var deviceDto in message.Devices)
            {
                if (deviceDto.IsConnected)
                {
                    Console.WriteLine($"Connected device is {deviceDto.Name}");
                    return Task.CompletedTask;
                }
            }

            return Task.CompletedTask;
        }

        static Task OnDeviceConnectionStateChanged(DeviceConnectionStateChangedMessage message)
        {
            Console.WriteLine($"Device {message.Device.Mac} connection state changed: IsConnected - {message.Device.IsConnected}");
            
            return Task.CompletedTask;
        }

        async static Task OnConnectedToServer(ConnectedToServerEvent arg)
        {
            await _metaMessenger.PublishOnServer(new RefreshServiceInfoMessage());
        }
    }
}
