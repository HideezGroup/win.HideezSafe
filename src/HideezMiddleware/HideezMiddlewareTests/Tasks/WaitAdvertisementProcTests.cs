using AutoFixture;
using AutoFixture.AutoMoq;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.Tasks;
using Moq;
using NUnit.Framework;
using System.Threading.Tasks;

namespace HideezMiddleware.Tests.Tasks
{
    [Parallelizable(ParallelScope.All)]
    class WaitAdvertisementProcTests
    {
        [Test]
        public async Task RunProcWithFilter_NoFilteredDevices_NullReturned()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = fixture.Create<AdvertismentReceivedEventArgs>();

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();

            var proximitySettingsProviderMock = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProviderMock.Setup(mock => mock.GetUnlockProximity(It.IsAny<ConnectionId>())).Returns(SdkConfig.DefaultUnlockProximity);

            var waitAdvProc = new WaitAdvertisementProc(bleConnectionManagerMock.Object, (e) => false);

            // Act
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);
            });
            var res = await waitAdvProc.Run(2_000);

            // Assert
            Assert.IsNull(res);
        }

        [Test]
        public async Task RunProcWithFilter_FoundDevice_AdvReturned()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                (sbyte)BleUtils.ProximityToRssi(SdkConfig.DefaultUnlockProximity));

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();

            var proximitySettingsProviderMock = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProviderMock.Setup(mock => mock.GetUnlockProximity(It.IsAny<ConnectionId>())).Returns(SdkConfig.DefaultUnlockProximity);

            var waitAdvProc = new WaitAdvertisementProc(bleConnectionManagerMock.Object, (e) => true);

            // Act
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);
            });
            var res = await waitAdvProc.Run(2_000);

            // Assert
            Assert.AreEqual(advEventArgs, res);
        }

        [Test]
        public async Task RunProcWithFilter_TwoDevicesAdvertise_LastAdvReturned()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });
            
            var lastAdvEventArgs = fixture.Create<AdvertismentReceivedEventArgs>();
            var firstAdvEventArgs = fixture.Create<AdvertismentReceivedEventArgs>();

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();

            var proximitySettingsProviderMock = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProviderMock.Setup(mock => mock.GetUnlockProximity(It.Is<ConnectionId>(x => x.Id == firstAdvEventArgs.Id)))
                .Returns(SdkConfig.DefaultUnlockProximity);

            var waitAdvProc = new WaitAdvertisementProc(bleConnectionManagerMock.Object, (e) => true);

            // Act
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, firstAdvEventArgs);
            });
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, lastAdvEventArgs);
            });
            var res = await waitAdvProc.Run(2_000);

            // Assert
            Assert.AreEqual(lastAdvEventArgs, res);
        }

        [Test]
        public async Task RunProc_AdvertisementReceived_AdvReturned()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = fixture.Create<AdvertismentReceivedEventArgs>();

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();

            var waitAdvProc = new WaitAdvertisementProc(bleConnectionManagerMock.Object);

            // Act
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);
            });
            var res = await waitAdvProc.Run(2_000);

            // Assert
            Assert.AreEqual(advEventArgs, res);
        }

        [Test]
        public async Task RunProc_AdvertisementNotReceived_NullReturned()
        {
            // Arrange
            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();

            var waitAdvProc = new WaitAdvertisementProc(bleConnectionManagerMock.Object);

            // Act
            var res = await waitAdvProc.Run(2_000);

            // Assert
            Assert.IsNull(res);
        }
    }
}
