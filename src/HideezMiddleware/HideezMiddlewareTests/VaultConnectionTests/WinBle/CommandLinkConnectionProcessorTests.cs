﻿using AutoFixture;
using AutoFixture.AutoMoq;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
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

            var proximitySettingsProviderMock = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsProviderMock.Setup(mock => mock.IsEnabledUnlock(It.IsAny<ConnectionId>())).Returns(true);
            proximitySettingsProviderMock.Setup(mock => mock.GetUnlockProximity(It.IsAny<ConnectionId>())).Returns(SdkConfig.DefaultUnlockProximity);
            fixture.Inject(proximitySettingsProviderMock.Object);

            var workstationUnlockMock = new Mock<IWorkstationUnlocker>();
            workstationUnlockMock.Setup(mock => mock.IsConnected).Returns(true);
            fixture.Inject(workstationUnlockMock.Object);

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
            proximitySettingsProviderMock.Setup(mock => mock.IsEnabledUnlock(It.IsAny<ConnectionId>())).Returns(true);
            proximitySettingsProviderMock.Setup(mock => mock.GetUnlockProximity(It.IsAny<ConnectionId>())).Returns(SdkConfig.DefaultUnlockProximity);
            fixture.Inject(proximitySettingsProviderMock.Object);

            var workstationUnlockMock = new Mock<IWorkstationUnlocker>();
            workstationUnlockMock.Setup(mock => mock.IsConnected).Returns(true);
            fixture.Inject(workstationUnlockMock.Object);

            var commandLinkConnectionProcessor = fixture.Create<CommandLinkConnectionProcessor>();

            commandLinkConnectionProcessor.Start();

            // Act
            workstationUnlockMock.Raise(mock => mock.CommandLinkPressed += null, EventArgs.Empty);
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
            proximitySettingsProviderMock.Setup(mock => mock.IsEnabledUnlock(It.IsAny<ConnectionId>())).Returns(true);
            proximitySettingsProviderMock.Setup(mock => mock.GetUnlockProximity(It.IsAny<ConnectionId>())).Returns(SdkConfig.DefaultUnlockProximity);
            fixture.Inject(proximitySettingsProviderMock.Object);

            var workstationUnlockMock = new Mock<IWorkstationUnlocker>();
            workstationUnlockMock.Setup(mock => mock.IsConnected).Returns(true);
            fixture.Inject(workstationUnlockMock.Object);

            var commandLinkConnectionProcessor = fixture.Create<CommandLinkConnectionProcessor>();

            commandLinkConnectionProcessor.Start();

            // Act
            workstationUnlockMock.Raise(mock => mock.CommandLinkPressed += null, EventArgs.Empty);
            bleConnectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, advEventArgs);

            // Assert
            connectionFlowProcessorMock.Verify(mock => mock.ConnectAndUnlock(
                It.IsAny<ConnectionId>(),
                It.IsAny<Action<WorkstationUnlockResult>>()),
                Times.Never);
        }
    }
}
