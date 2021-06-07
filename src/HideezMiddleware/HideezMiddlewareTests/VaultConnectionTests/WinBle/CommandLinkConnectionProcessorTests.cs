using AutoFixture;
using AutoFixture.AutoMoq;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.WinBle;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using Moq;
using NUnit.Framework;
using System;

namespace HideezMiddleware.Tests.VaultConnectionTests.WinBle
{
    class CommandLinkConnectionProcessorTests
    {
        [Test]
        public void Unlock_ProcessorDisabled_LinkIgnored()
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });
            
            var advEventArgs = new AdvertismentReceivedEventArgs(fixture.Create<string>(),
                fixture.Create<string>(),
                (sbyte)(SdkConfig.TapProximityUnlockThreshold + 1));

            var bleConnectionManagerMock = new Mock<IBleConnectionManager>();
            fixture.Inject(bleConnectionManagerMock.Object);

            var workstationUnlockerProxy = new Mock<IWorkstationUnlocker>();
            workstationUnlockerProxy.SetupGet(mock => mock.IsConnected).Returns(true);
            fixture.Inject(workstationUnlockerProxy.Object);

            var connectionFlowProcessorMock = new Mock<IConnectionFlowProcessor>();
            fixture.Inject(connectionFlowProcessorMock.Object);

            var proximitySettingsProvider = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProvider.Setup(mock => mock.IsEnabledUnlock(It.IsAny<ConnectionId>())).Returns(true);
            proximitySettingsProvider.Setup(mock => mock.GetUnlockProximity(It.IsAny<ConnectionId>())).Returns(SdkConfig.DefaultUnlockProximity);
            fixture.Inject(proximitySettingsProvider.Object);

            var commandLinkConnectionProcessor = fixture.Create<CommandLinkConnectionProcessor>();

            // Act
            workstationUnlockerProxy.Raise(mock => mock.CommandLinkPressed += null, EventArgs.Empty);
            bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);

            // Assert
            connectionFlowProcessorMock.Verify(mock => mock.ConnectAndUnlock(
                It.IsAny<ConnectionId>(),
                It.IsAny<Action<WorkstationUnlockResult>>()),
                Times.Never);
        }
    }
}
