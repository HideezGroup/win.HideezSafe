using AutoFixture;
using AutoFixture.AutoMoq;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.Tasks;
using Moq;
using NUnit.Framework;
using System.Threading.Tasks;

namespace HideezMiddleware.Tests.Tasks
{
    class WaitAdvertisementProcTests
    {
        [Test]
        public async Task RunProc_NoDeviceWithEnabledUnlockByActivity_NullReturned()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                fixture.Create<sbyte>());

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var proximitySettingsProviderMock = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProviderMock.Setup(mock => mock.IsEnabledUnlockByActivity(It.IsAny<ConnectionId>())).Returns(false);
            fixture.Inject(proximitySettingsProviderMock.Object);

            var waitAdvProc = new WaitAdvertisementProc(bleConnectionManagerMock.Object, proximitySettingsProviderMock.Object);

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
        public async Task RunProc_FoundDeviceWithEnabledUnlockByActivity_AdvReturned()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                fixture.Create<sbyte>());

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var proximitySettingsProviderMock = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProviderMock.Setup(mock => mock.IsEnabledUnlockByActivity(It.IsAny<ConnectionId>())).Returns(true);
            fixture.Inject(proximitySettingsProviderMock.Object);

            var waitAdvProc = new WaitAdvertisementProc(bleConnectionManagerMock.Object, proximitySettingsProviderMock.Object);

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
        public async Task RunProc_AdvertisementReceived_AdvReturned()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                fixture.Create<sbyte>());

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

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
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var waitAdvProc = new WaitAdvertisementProc(bleConnectionManagerMock.Object);

            // Act
            var res = await waitAdvProc.Run(2_000);

            // Assert
            Assert.IsNull(res);
        }
    }
}
