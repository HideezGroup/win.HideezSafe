using AutoFixture;
using AutoFixture.AutoMoq;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using HideezMiddleware.Utils.WorkstationHelper;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.Tests.VaultConnectionTests
{
    [Parallelizable(ParallelScope.All)]
    class EnterpriseConnectionFlowProcessorTests
    {
        IConnectionController GetConnectionController(ConnectionId connectionId)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });
            fixture.Inject(connectionId);
            fixture.Inject(ConnectionState.Connected);
            return fixture.Create<IConnectionController>();
        }

        EnterpriseConnectionFlowProcessor.ConnectionFlowSubprocessorsStruct GetConnectionFlowSubprocessors(
            HesConnectionState hesConnectionState,
            ushort licenseCount,
            bool isMasterKeyRequired,
            bool isLinkRequired,
            bool isLocked,
            DeviceManager deviceManager,
            IConnectionController connectionController)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            var deviceMock = new Mock<IDevice>();
            deviceMock.SetupGet(d => d.FirmwareVersion).Returns(fixture.Create<Version>());
            deviceMock.SetupGet(d => d.Id).Returns(fixture.Create<string>());
            deviceMock.SetupGet(d => d.DeviceConnection).Returns(connectionController);
            deviceMock.SetupGet(d => d.ChannelNo).Returns((byte)DefaultDeviceChannel.Main);

            var vaultConnectionProcessorMock = new Mock<IVaultConnectionProcessor>();
            vaultConnectionProcessorMock.Setup(p =>
                p.ConnectVault(connectionController.Connection.ConnectionId, false, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(deviceMock.Object)).
                Callback(async () => await deviceManager.Connect(connectionController.Connection.ConnectionId));
            vaultConnectionProcessorMock.Setup(p =>
                p.WaitVaultInitialization(deviceMock.Object, It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    deviceMock.SetupGet(d => d.LicenseInfo).Returns(licenseCount);
                    deviceMock.SetupGet(d => d.Id).Returns(deviceManager.Devices.FirstOrDefault().Id);
                    deviceMock.SetupGet(d => d.AccessLevel).Returns(new AccessLevel(isLinkRequired, false, isMasterKeyRequired, false, false, isLocked));
                    deviceMock.SetupGet(d => d.IsLocked).Returns(isLocked);
                });
            fixture.Inject(vaultConnectionProcessorMock.Object);

            if (licenseCount == 0)
            {
                var licensingProcessorMock = new Mock<ILicensingProcessor>();
                licensingProcessorMock.Setup(p =>
                    p.CheckLicense(deviceMock.Object, It.IsAny<HwVaultInfoFromHesDto>(), It.IsAny<CancellationToken>()))
                    .Throws(new WorkflowException());
                fixture.Inject(licensingProcessorMock.Object);
            }

            if (hesConnectionState != HesConnectionState.Connected)
            {
                if (isMasterKeyRequired)
                {
                    Mock<IVaultAuthorizationProcessor> vaultAuthorizationProcessorMock = new Mock<IVaultAuthorizationProcessor>();
                    vaultAuthorizationProcessorMock.Setup(p =>
                        p.AuthVault(deviceMock.Object, It.IsAny<CancellationToken>()))
                        .Throws(new WorkflowException());
                    fixture.Inject(vaultAuthorizationProcessorMock.Object);
                }
                else if (isLinkRequired)
                {
                    Mock<IStateUpdateProcessor> stateUpdateProcessorMock = new Mock<IStateUpdateProcessor>();
                    stateUpdateProcessorMock.Setup(p =>
                        p.UpdateVaultStatus(deviceMock.Object, It.IsAny<HwVaultInfoFromHesDto>(), It.IsAny<CancellationToken>()))
                        .Throws(new WorkflowException());
                    fixture.Inject(stateUpdateProcessorMock.Object);
                }
                else if (isLocked)
                {
                    Mock<IActivationProcessor> activationProcessorMock = new Mock<IActivationProcessor>();
                    activationProcessorMock.Setup(p => p.ActivateVault(deviceMock.Object, It.IsAny<HwVaultInfoFromHesDto>(), It.IsAny<CancellationToken>()))
                            .Throws(new WorkflowException());
                    fixture.Inject(activationProcessorMock.Object);
                }
            }

            return fixture.Create<EnterpriseConnectionFlowProcessor.ConnectionFlowSubprocessorsStruct>();
        }


        [Test]
        [TestCase((ushort)1, true, false, false, 0)]
        [TestCase((ushort)1, false, true, false, 0)]
        [TestCase((ushort)1, false, false, true, 0)]
        [TestCase((ushort)0, false, false, false, 0)]
        [TestCase((ushort)1, false, false, false, 0)]
        public async Task TryConnect_CheckNeedDeleteBond_DeviceRemovedInvokedExpectedTimes(
            ushort licenseCount,
            bool isMasterKeyRequired,
            bool isLinkRequired,
            bool isLocked,
            int expectedResult)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });

            //Arrange
            ConnectionId connectionId = new ConnectionId(fixture.Create<string>(), (byte)DefaultConnectionIdProvider.WinBle);
            HesConnectionState hesConnectionState = HesConnectionState.Disconnected;

            Mock<IHesAppConnection> hesAppConnectionMock = new Mock<IHesAppConnection>();
            hesAppConnectionMock.SetupGet(c => c.State).Returns(hesConnectionState);
            fixture.Inject(hesAppConnectionMock.Object);

            Mock<IWorkstationHelper> workstationHelperMock = new Mock<IWorkstationHelper>();
            workstationHelperMock.Setup(w => w.IsActiveSessionLocked()).Returns(false);
            fixture.Inject(workstationHelperMock.Object);

            var controller = GetConnectionController(connectionId);

            Mock<IConnectionManager> connectionManagerMock = new Mock<IConnectionManager>();
            connectionManagerMock.SetupGet(m => m.Id).Returns((byte)DefaultConnectionIdProvider.WinBle);
            connectionManagerMock.Setup(m => m.Connect(connectionId)).Returns(Task.FromResult(controller))
                .Callback(() => connectionManagerMock.Raise(m => m.ControllerAdded += null, new ControllerAddedEventArgs(controller)));
            connectionManagerMock.Setup(m => m.DeleteBond(controller))
                .Callback(() => connectionManagerMock.Raise(m => m.ControllerRemoved += null, new ControllerRemovedEventArgs(controller)));

            var coordinator = new ConnectionManagersCoordinator();
            coordinator.AddConnectionManager(connectionManagerMock.Object);
            fixture.Inject(coordinator);

            int invokeCounter = 0;
            DeviceManager deviceManager = fixture.Create<DeviceManager>();
            deviceManager.DeviceRemoved += (sender, e) => invokeCounter++;

            var connectionFlowSubprocessors = GetConnectionFlowSubprocessors(
                hesConnectionState,
                licenseCount,
                isMasterKeyRequired,
                isLinkRequired,
                isLocked,
                deviceManager,
                controller);
            fixture.Inject(connectionFlowSubprocessors);

            var connectionFlowProcessor = fixture.Create<EnterpriseConnectionFlowProcessor>();

            //Act
            await connectionFlowProcessor.Connect(connectionId);
            await (Task.Delay(1000));

            //Assert
            Assert.AreEqual(expectedResult, invokeCounter);
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
            deviceMock.SetupGet(d => d.LicenseInfo).Returns(1);
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
                p.ConnectVault(connectionId, false, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(deviceMock.Object));
            fixture.Inject(vaultConnectionProcessorMock.Object);

            bool workflowStarted = false;
            bool deviceFinishedWorkflow = false;
            bool workflowFinished = false;
            var connectionFlowProcessor = fixture.Create<EnterpriseConnectionFlowProcessor>();
            connectionFlowProcessor.Started += (e, a) => workflowStarted = true;
            connectionFlowProcessor.DeviceFinishedMainFlow += (e, a) => deviceFinishedWorkflow = true;
            connectionFlowProcessor.Finished += (e, a) => workflowFinished = true;

            // Act
            await connectionFlowProcessor.Connect(connectionId);
            await (Task.Delay(1000));

            //Assert
            Assert.IsTrue(workflowStarted);
            Assert.IsTrue(deviceFinishedWorkflow);
            Assert.IsTrue(workflowFinished);
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
            deviceMock.SetupGet(d => d.LicenseInfo).Returns(1);
            deviceMock.SetupGet(d => d.AccessLevel).Returns(new AccessLevel(false, false, false, false, false, false));
            deviceMock.SetupGet(d => d.IsConnected).Returns(true);
            deviceMock.SetupGet(d => d.IsInitialized).Returns(true);
            deviceMock.SetupGet(d => d.ChannelNo).Returns((byte)DefaultDeviceChannel.Main);
            deviceMock.Setup(d => d.GetUserProperty<bool>(WorkflowProperties.HV_FINISHED_WF)).Returns(true);

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
