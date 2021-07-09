using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using Nito.AsyncEx;
using System;
using System.Threading.Tasks;
using Hideez.Integration.Lite.Enums;
using Hideez.Integration.Lite.Messages;

namespace ButtonPressTrackingSample
{
    /// <summary>
    /// This sample shows how to connect to installed Hideez Service,
    /// monitor devices collection change and track device button events.
    /// </summary>
    class Program
    {
        // This object is responsible for communication with Hideez Service.
        static IMetaPubSub _metaMessenger = new MetaPubSub();

        static void Main(string[] args)
        {
            AsyncContext.Run(() => MainAsync(args));
        }

        static async void MainAsync(string[] args)
        {
            // Subscribing to message that is sent when connection with Hideez Service is successfully established.
            // This message is sent again every time connection is re-established. 
            // For example, after service was restarted.
            _metaMessenger.Subscribe<ConnectedToServerEvent>(OnConnectedToServer); 
                
            // This establishes connection with Hideez Service and should be called only once.
            // If connection gets disrupted, it will be restored automatically as soon as possible.
            await _metaMessenger.TryConnectToServer("HideezServicePipe");

            // Ask Hideez Service to start sending us following messages.
            // If connection gets disrupted, re-subscription happens automatically as soon as possible.
            await _metaMessenger.TrySubscribeOnServer<DevicesCollectionChangedMessage>(OnDevicesCollectionChanged);
            await _metaMessenger.TrySubscribeOnServer<DeviceConnectionStateChangedMessage>(OnDeviceConnectionStateChanged);
            await _metaMessenger.TrySubscribeOnServer<DeviceStateChangedMessage>(OnDeviceStateChanged);

            try
            {
                // Send message to Hideez Service that triggers immediate broadcast of 
                // certain messages, namely DevicesCollectionChangedMessage.
                await _metaMessenger.PublishOnServer(new RefreshServiceInfoMessage());
            }
            catch (Exception ex)
            {
                // PublishOnServer may throw an error if connection is not established or was disrupted.
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("Press 'Enter' to exit");
            Console.ReadLine();
        }

        private static Task OnDeviceStateChanged(DeviceStateChangedMessage arg)
        {
            // DeviceStateChangedMessage.Button will indicate press code only once per button press event
            // and immediately reverts back to equivalent of ButtonPressCode.None for subsequent messages.
            // Multiple button presses in quick succession are aggregated into one press code. (Double, Triple etc)
            var buttonPressCode = (ButtonPressCode)arg.Button;
            if (buttonPressCode != ButtonPressCode.None)
                Console.WriteLine($"Device ({arg.DeviceId}) button was pressed, press code: {buttonPressCode}");

            return Task.CompletedTask;
        }

        static Task OnDevicesCollectionChanged(DevicesCollectionChangedMessage message)
        {
            // DevicesCollectionChangedMessage always returns actual state of devices collection
            Console.WriteLine("Devices collection changed. Current devices:");

            foreach (var deviceDto in message.Devices)
                Console.WriteLine($"{deviceDto.Name} - {deviceDto.Id} - {(deviceDto.IsConnected ? "Connected" : "Disconnected" )}");

            return Task.CompletedTask;
        }

        static Task OnDeviceConnectionStateChanged(DeviceConnectionStateChangedMessage message)
        {
            // ChannelNo == 1 represents physical device. 
            // ChannelNo 2 to 6 represents logical devices that share physical connection with ChannelNo 1
            if (message.Device.ChannelNo == 1)
                Console.WriteLine($"Device {message.Device.Mac} connection state " +
                    $"changed to {(message.Device.IsConnected ? "Connected" : "Disconnected")}");
            
            return Task.CompletedTask;
        }

        async static Task OnConnectedToServer(ConnectedToServerEvent arg)
        {
            Console.WriteLine("Successfully connected to Hideez Service");
            await _metaMessenger.PublishOnServer(new RefreshServiceInfoMessage());
        }
    }
}
