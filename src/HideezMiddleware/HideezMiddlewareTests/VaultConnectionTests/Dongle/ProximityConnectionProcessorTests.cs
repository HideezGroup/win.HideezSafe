using AutoFixture;
using AutoFixture.AutoMoq;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.Dongle;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using Moq;
using NUnit.Framework;
using System;

namespace HideezMiddleware.Tests.VaultConnectionTests.Dongle
{
    class ProximityConnectionProcessorTests
    {
        [Test]
        public void Unlock_ProcessorDisabled_AdvertisementIgnored()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                (sbyte)SdkConfig.TapProximityUnlockThreshold);

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var connectionFlowProcessorMock = new Mock<IConnectionFlowProcessor>();
            fixture.Inject(connectionFlowProcessorMock.Object);

            var proximityConnectionProcessor = fixture.Create<ProximityConnectionProcessor>();

            // Act
            bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);

            // Assert
            connectionFlowProcessorMock.Verify(mock => mock.ConnectAndUnlock(
                It.IsAny<ConnectionId>(),
                It.IsAny<Action<WorkstationUnlockResult>>()),
                Times.Never);
        }
    }
}
