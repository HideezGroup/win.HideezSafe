using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using Meta.Lib.Utils;
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
                _metaMessenger.Subscribe<ConnectedToServerEvent>(OnConnectedToServer);
                await _metaMessenger.TrySubscribeOnServer<DevicesCollectionChangedMessage>(OnDevicesCollectionChanged);
                await _metaMessenger.TrySubscribeOnServer<DeviceConnectionStateChangedMessage>(OnDeviceConnectionStateChanged);
                await _metaMessenger.TrySubscribeOnServer<DeviceStateChangedMessage>(OnDeviceStateChanged);
                await _metaMessenger.TryConnectToServer("HideezServicePipe");

                var connectedDevice = await WaitConnection();
                if (connectedDevice != null)
                {
                    Console.WriteLine($"Connected device is {connectedDevice.Name}");

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

        private static Task OnDeviceStateChanged(DeviceStateChangedMessage arg)
        {
            if (arg.Button != ButtonPressCode.None)
            {
                Console.WriteLine($"Vault {arg.DeviceId} button pressed, code: {arg.Button}");
                _buttonPressTcs.TrySetResult(true);
            }
            return Task.CompletedTask;
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

        async static Task OnConnectedToServer(ConnectedToServerEvent arg)
        {
            await _metaMessenger.PublishOnServer(new RefreshServiceInfoMessage());
        }
    }
}
