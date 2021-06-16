using AutoFixture;
using AutoFixture.AutoMoq;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using HideezMiddleware.Utils.WorkstationHelper;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.Tests.VaultConnectionTests
{
    [Parallelizable(ParallelScope.All)]
    class StandaloneConnectionFlowProcessorTests
    {
        IConnectionController GetConnectionController(ConnectionId connectionId)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });
            fixture.Inject(connectionId);
            fixture.Inject(ConnectionState.Connected);
            return fixture.Create<IConnectionController>();
        }

        [Test]
        public async Task TryConnect_GhostedDevice_WorkflowPerformed([Values] DefaultConnectionIdProvider connectionIdProvider)
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var connectionId = new ConnectionId(fixture.Create<string>(), (byte)connectionIdProvider);
            var connectionController = GetConnectionController(connectionId);
            var deviceMock = new Mock<IDevice>();
            deviceMock.SetupGet(d => d.Id).Returns(fixture.Create<string>());
            deviceMock.SetupGet(d => d.DeviceConnection).Returns(connectionController);
            deviceMock.SetupGet(d => d.LicenseInfo).Returns(0);
            deviceMock.SetupGet(d => d.AccessLevel).Returns(new AccessLevel(false, false, false, false, false, false));
            deviceMock.SetupGet(d => d.IsConnected).Returns(true);
            deviceMock.SetupGet(d => d.IsInitialized).Returns(true);
            deviceMock.SetupGet(d => d.ChannelNo).Returns((byte)DefaultDeviceChannel.Main);

            var devicesList = new List<IDevice>()
            {
                deviceMock.Object
            };

            var deviceManagerMock = new Mock<IDeviceManager>();
            deviceManagerMock.SetupGet(mock => mock.Devices).Returns(devicesList.AsReadOnly());
            fixture.Inject(deviceManagerMock.Object);

            var workstationHelperMock = new Mock<IWorkstationHelper>();
            workstationHelperMock.Setup(x => x.IsActiveSessionLocked()).Returns(false);
            fixture.Inject(workstationHelperMock.Object);

            var vaultConnectionProcessorMock = new Mock<IVaultConnectionProcessor>();
            vaultConnectionProcessorMock.Setup(p =>
                p.ConnectVault(It.IsAny<ConnectionId>(), false, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(deviceMock.Object));
            fixture.Inject(vaultConnectionProcessorMock.Object);

            bool deviceFinishedWorkflow = false;
            var connectionFlowProcessor = fixture.Create<StandaloneConnectionFlowProcessor>();
            connectionFlowProcessor.DeviceFinishedMainFlow += (e, a) => deviceFinishedWorkflow = true;

            // Act
            await connectionFlowProcessor.Connect(connectionId);
            await (Task.Delay(1000));

            //Assert
            Assert.IsTrue(deviceFinishedWorkflow);
        }

        [Test]
        public async Task TryConnect_DeviceFinishedWorkflow_WorkflowCancelled([Values] DefaultConnectionIdProvider connectionIdProvider)
        {
            // Arrange
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var connectionId = new ConnectionId(fixture.Create<string>(), (byte)connectionIdProvider);
            var connectionController = GetConnectionController(connectionId);
            var deviceMock = new Mock<IDevice>();
            deviceMock.SetupGet(d => d.Id).Returns(fixture.Create<string>());
            deviceMock.SetupGet(d => d.DeviceConnection).Returns(connectionController);
            deviceMock.SetupGet(d => d.LicenseInfo).Returns(0);
            deviceMock.SetupGet(d => d.AccessLevel).Returns(new AccessLevel(false, false, false, false, false, false));
            deviceMock.SetupGet(d => d.IsConnected).Returns(true);
            deviceMock.SetupGet(d => d.IsInitialized).Returns(true);
            deviceMock.SetupGet(d => d.ChannelNo).Returns((byte)DefaultDeviceChannel.Main);
            deviceMock.Setup(d => d.GetUserProperty<bool>(DeviceCustomProperties.HV_FINISHED_WF)).Returns(true);

            var devicesList = new List<IDevice>()
            {
                deviceMock.Object
            };

            var deviceManagerMock = new Mock<IDeviceManager>();
            deviceManagerMock.SetupGet(mock => mock.Devices).Returns(devicesList.AsReadOnly());
            fixture.Inject(deviceManagerMock.Object);

            var workstationHelperMock = new Mock<IWorkstationHelper>();
            workstationHelperMock.Setup(x => x.IsActiveSessionLocked()).Returns(false);
            fixture.Inject(workstationHelperMock.Object);

            var vaultConnectionProcessorMock = new Mock<IVaultConnectionProcessor>();
            vaultConnectionProcessorMock.Setup(p =>
                p.ConnectVault(connectionId, false, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(deviceMock.Object));
            fixture.Inject(vaultConnectionProcessorMock.Object);

            bool workflowStarted = false;
            var connectionFlowProcessor = fixture.Create<EnterpriseConnectionFlowProcessor>();
            connectionFlowProcessor.Started += (e, a) => workflowStarted = true;

            // Act
            await connectionFlowProcessor.Connect(connectionId);
            await (Task.Delay(1000));

            //Assert
            Assert.IsFalse(workflowStarted);
        }
    }
}
