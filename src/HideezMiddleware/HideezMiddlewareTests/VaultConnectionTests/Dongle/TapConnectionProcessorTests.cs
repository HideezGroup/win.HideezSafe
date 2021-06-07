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
    class TapConnectionProcessorTests
    {
        [Test]
        public void Unlock_ProcessorDisabled_AdvertisementIgnored()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                (sbyte)(SdkConfig.TapProximityUnlockThreshold + 1));
            
            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var connectionFlowProcessorMock = new Mock<IConnectionFlowProcessor>();
            fixture.Inject(connectionFlowProcessorMock.Object);

            var tapConnectionProcessor = fixture.Create<TapConnectionProcessor>();

            // Act
            bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);

            // Assert
            connectionFlowProcessorMock.Verify(mock => mock.ConnectAndUnlock(
                It.IsAny<ConnectionId>(),
                It.IsAny<Action<WorkstationUnlockResult>>()),
                Times.Never);
        }

        [Test]
        public void Unlock_AdvertisementReceived_UnlockInitiated()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                (sbyte)(SdkConfig.TapProximityUnlockThreshold + 1));

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var connectionFlowProcessorMock = new Mock<IConnectionFlowProcessor>();
            fixture.Inject(connectionFlowProcessorMock.Object);

            var tapConnectionProcessor = fixture.Create<TapConnectionProcessor>();

            tapConnectionProcessor.Start();

            // Act
            bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);

            // Assert
            connectionFlowProcessorMock.Verify(mock => mock.ConnectAndUnlock(
                It.Is<ConnectionId>(c => c.Id == advEventArgs.Id),
                It.IsAny<Action<WorkstationUnlockResult>>()),
                Times.Once);
        }

        [Test]
        public void Unlock_AdvertisementBelowThreshold_UnlockSkipped()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                (sbyte)(SdkConfig.TapProximityUnlockThreshold - 1));

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var connectionFlowProcessorMock = new Mock<IConnectionFlowProcessor>();
            fixture.Inject(connectionFlowProcessorMock.Object);

            var tapConnectionProcessor = fixture.Create<TapConnectionProcessor>();

            tapConnectionProcessor.Start();

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
