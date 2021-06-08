using AutoFixture;
using AutoFixture.AutoMoq;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.Other;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using Moq;
using NUnit.Framework;
using System;

namespace HideezMiddleware.Tests.VaultConnectionTests.Other
{
    class ActivityConnectionProcessorTests
    {
        [Test]
        public void Unlock_ProcessorDisabled_AdvertisementIgnored()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                (sbyte)BleUtils.ProximityToRssi(SdkConfig.DefaultUnlockProximity));

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var connectionFlowProcessorMock = new Mock<IConnectionFlowProcessor>();
            fixture.Inject(connectionFlowProcessorMock.Object);

            var proximitySettingsProviderMock = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProviderMock.Setup(mock => mock.GetUnlockProximity(It.IsAny<ConnectionId>())).Returns(SdkConfig.DefaultUnlockProximity);
            proximitySettingsProviderMock.Setup(mock => mock.IsEnabledUnlockByActivity(It.IsAny<ConnectionId>())).Returns(true);
            fixture.Inject(proximitySettingsProviderMock.Object);

            var workstationUnlockMock = new Mock<IWorkstationUnlocker>();
            workstationUnlockMock.Setup(mock => mock.IsConnected).Returns(true);
            fixture.Inject(workstationUnlockMock.Object);

            var activityConnectionProcessor = fixture.Create<ActivityConnectionProcessor>();

            // Act
            workstationUnlockMock.Raise(mock => mock.ProviderActivated += null, EventArgs.Empty);
            bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);

            // Assert
            connectionFlowProcessorMock.Verify(mock => mock.ConnectAndUnlock(
                It.IsAny<ConnectionId>(),
                It.IsAny<Action<WorkstationUnlockResult>>()),
                Times.Never);
        }

        [Test]
        public void Unlock_AdvProxAboveThreshold_UnlockInitiated()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                (sbyte)BleUtils.ProximityToRssi(SdkConfig.DefaultUnlockProximity));

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var connectionFlowProcessorMock = new Mock<IConnectionFlowProcessor>();
            fixture.Inject(connectionFlowProcessorMock.Object);

            var proximitySettingsProviderMock = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProviderMock.Setup(mock => mock.GetUnlockProximity(It.IsAny<ConnectionId>())).Returns(SdkConfig.DefaultUnlockProximity);
            proximitySettingsProviderMock.Setup(mock => mock.IsEnabledUnlockByActivity(It.IsAny<ConnectionId>())).Returns(true);
            fixture.Inject(proximitySettingsProviderMock.Object);

            var workstationUnlockMock = new Mock<IWorkstationUnlocker>();
            workstationUnlockMock.Setup(mock => mock.IsConnected).Returns(true);
            fixture.Inject(workstationUnlockMock.Object);

            var activityConnectionProcessor = fixture.Create<ActivityConnectionProcessor>();

            activityConnectionProcessor.Start();

            // Act
            workstationUnlockMock.Raise(mock => mock.ProviderActivated += null, EventArgs.Empty);
            bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);

            // Assert
            connectionFlowProcessorMock.Verify(mock => mock.ConnectAndUnlock(
                It.Is<ConnectionId>(c => c.Id == advEventArgs.Id),
                It.IsAny<Action<WorkstationUnlockResult>>()),
                Times.Once);
        }

        [Test]
        public void Unlock_AdvertisementProximiryAboveThreshold_UnlockSkipped()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                (sbyte)BleUtils.ProximityToRssi(SdkConfig.DefaultUnlockProximity - 1));

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var connectionFlowProcessorMock = new Mock<IConnectionFlowProcessor>();
            fixture.Inject(connectionFlowProcessorMock.Object);

            var proximitySettingsProviderMock = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProviderMock.Setup(mock => mock.GetUnlockProximity(It.IsAny<ConnectionId>())).Returns(SdkConfig.DefaultUnlockProximity);
            proximitySettingsProviderMock.Setup(mock => mock.IsEnabledUnlockByActivity(It.IsAny<ConnectionId>())).Returns(true);
            fixture.Inject(proximitySettingsProviderMock.Object);

            var workstationUnlockMock = new Mock<IWorkstationUnlocker>();
            workstationUnlockMock.Setup(mock => mock.IsConnected).Returns(true);
            fixture.Inject(workstationUnlockMock.Object);

            var activityConnectionProcessor = fixture.Create<ActivityConnectionProcessor>();

            activityConnectionProcessor.Start();

            // Act
            workstationUnlockMock.Raise(mock => mock.ProviderActivated += null, EventArgs.Empty);
            bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);

            // Assert
            connectionFlowProcessorMock.Verify(mock => mock.ConnectAndUnlock(
                It.IsAny<ConnectionId>(),
                It.IsAny<Action<WorkstationUnlockResult>>()),
                Times.Never);
        }
    }
}
