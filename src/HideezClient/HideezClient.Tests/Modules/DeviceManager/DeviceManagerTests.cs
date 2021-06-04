using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using HideezMiddleware.IPC.DTO;
using Meta.Lib.Modules.PubSub;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.ApplicationModeProvider;
using AutoFixture;
using AutoFixture.AutoMoq;

namespace HideezClient.Modules.DeviceManager.Tests
{
    [Parallelizable(ParallelScope.All)]
    public class DeviceManagerTests
    {
        [Test]
        public async Task EnumerateDevices_FluctuatingServiceConnection_DevicesEnumerated()
        {
            var devices = new List<DeviceDTO>();
            IMetaPubSub messenger = new MetaPubSub();
            IMetaPubSub hub = new MetaPubSub();
            hub.StartServer("Test1");

            int devicesCount = 100;

            for (int i = 0; i < devicesCount; i++)
                devices.Add(GetRandomDeviceDTO());

            int serviceReconnectsCount = 10;
            for (int i = 0; i < serviceReconnectsCount; i++)
            {
                await messenger.TryConnectToServer("Test1");
                IDeviceManager deviceManager = GetDeviceManager(messenger, devices);
                await Task.Delay(200);
                Assert.AreEqual(devicesCount, deviceManager.Devices.Count());

                await messenger.DisconnectFromServer();
                await Task.Delay(200);
                Assert.AreEqual(0, deviceManager.Devices.Count());
            }
        }

        [Test]
        public async Task DeviceCollectionChanged_AddDevices_DevicesEnumerated()
        {
            var devices = new List<DeviceDTO>();
            IMetaPubSub messenger = new MetaPubSub();
            IDeviceManager deviceManager = GetDeviceManager(messenger);

            await Task.Run(() =>
            {
                devices.Add(GetRandomDeviceDTO());
                devices.Add(GetRandomDeviceDTO());
                devices.Add(GetRandomDeviceDTO());
                messenger.Publish(new DevicesCollectionChangedMessage(devices.ToArray()));
            });

            await Task.Delay(5000);
            Assert.AreEqual(3, deviceManager.Devices.Count());

            await Task.Run(() =>
            {
                devices.RemoveAt(0);
            });
            await messenger.Publish(new DevicesCollectionChangedMessage(devices.ToArray()));
            await Task.Delay(200);
            Assert.AreEqual(2, deviceManager.Devices.Count());

            await Task.Run(() =>
            {
                devices.Clear();
            });
            await messenger.Publish(new DevicesCollectionChangedMessage(devices.ToArray()));
            await Task.Delay(500);
            Assert.AreEqual(0, deviceManager.Devices.Count());
        }

        [Test]
        public async Task DeviceCollectionChanged_AddDevicesAsync_DevicesEnumerated()
        {
            var devices = new List<DeviceDTO>();
            IMetaPubSub messenger = new MetaPubSub();

            devices.Add(GetRandomDeviceDTO());

            IDeviceManager deviceManager = GetDeviceManager(messenger, devices);
            Assert.AreEqual(1, deviceManager.Devices.Count());

            devices.Add(GetRandomDeviceDTO());
            await messenger.Publish(new DevicesCollectionChangedMessage(devices.ToArray()));

            await Task.Run(() =>
            {
                devices.Add(GetRandomDeviceDTO());
                devices.Add(GetRandomDeviceDTO());
                messenger.Publish(new DevicesCollectionChangedMessage(devices.ToArray()));
            });

            await Task.Delay(2000);
            Assert.AreEqual(4, deviceManager.Devices.Count());

            devices.RemoveAt(0);
            await messenger.Publish(new DevicesCollectionChangedMessage(devices.ToArray()));
            await Task.Delay(1000);
            Assert.AreEqual(3, deviceManager.Devices.Count());

            await Task.Run(() =>
            {
                devices.RemoveAt(0);
            });
            await messenger.Publish(new DevicesCollectionChangedMessage(devices.ToArray()));
            await Task.Delay(1000);
            Assert.AreEqual(2, deviceManager.Devices.Count());
        }

        [Test]
        public async Task EnumerateDevices_ClearDevices_DevicesCollectionCleared()
        {
            // Arrange
            var devices = new List<DeviceDTO>();
            IMetaPubSub messenger = new MetaPubSub();
            IMetaPubSub hub = new MetaPubSub();
            hub.StartServer("Test2");
            await messenger.TryConnectToServer("Test2");
            IDeviceManager deviceManager = GetDeviceManager(messenger);

            int devicesCount = 1000;
            for (int i = 0; i < devicesCount; i++)
                devices.Add(GetRandomDeviceDTO());

            await messenger.PublishOnServer(new DevicesCollectionChangedMessage(devices.ToArray()));

            // Act
            await messenger.DisconnectFromServer();
            await Task.Delay(200 + devicesCount * 5); // DeviceManager handles messenger disconnect asynchronously

            // Assert
            Assert.AreEqual(0, deviceManager.Devices.Count());
        }

        [Test]
        public async Task EnumerateDevices_QuickReconnect_DevicesCollectionEnumerated()
        {
            var devices = new List<DeviceDTO>();
            IMetaPubSub messenger = new MetaPubSub();
            IDeviceManager deviceManager = GetDeviceManager(messenger);
            IMetaPubSub hub = new MetaPubSub();
            hub.StartServer("Test3");

            int devicesCount = 1000;
            for (int i = 0; i < devicesCount; i++)
                devices.Add(GetRandomDeviceDTO());

            var connectionTask = Task.Factory.StartNew(()=>
            {
                messenger.TryConnectToServer("Test3");
                 deviceManager = GetDeviceManager(messenger, devices);
            });
            var disconnectionTask = Task.Factory.StartNew(messenger.DisconnectFromServer);
            var reconnectionTask = Task.Factory.StartNew(() => messenger.TryConnectToServer("Test3"));

            await Task.WhenAll(connectionTask, disconnectionTask, reconnectionTask);
            await Task.Delay(2000);
            Assert.AreEqual(devicesCount, deviceManager.Devices.Count());
        }

        private IDeviceManager GetDeviceManager(IMetaPubSub messanger)
        {
            return new DeviceManager(messanger, 
                new Mock<IWindowsManager>().Object, 
                new Mock<IRemoteDeviceFactory>().Object, 
                new Mock<IApplicationModeProvider>().Object);
        }

        private IDeviceManager GetDeviceManager(IMetaPubSub messanger, List<DeviceDTO> devices)
        {
            return new DeviceManager(messanger,
                new Mock<IWindowsManager>().Object,
                new Mock<IRemoteDeviceFactory>().Object,
                new Mock<IApplicationModeProvider>().Object,
                devices);
        }

        private DeviceDTO GetRandomDeviceDTO()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            return fixture.Build<DeviceDTO>()
                .With(d => d.ChannelNo, 1)
                .With(d => d.IsConnected, true)
                .With(d => d.SnapshotTime, DateTime.Now)
                .Create();
        }
    }
}