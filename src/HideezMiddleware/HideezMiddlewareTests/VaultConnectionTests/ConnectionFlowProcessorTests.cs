using AutoFixture;
using AutoFixture.AutoMoq;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using HideezMiddleware.DeviceLogging;
using HideezMiddleware.ScreenActivation;
using HideezMiddleware.Settings;
using HideezMiddleware.Utils.WorkstationHelper;
using Meta.Lib.Modules.PubSub;
using Moq;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.Tests.VaultConnectionTests
{
    class ConnectionFlowProcessorTests
    {
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
            //Arrange
            ConnectionId connectionId = new ConnectionId(It.IsAny<string>(), (byte)DefaultConnectionIdProvider.WinBle);
            HesConnectionState hesConnectionState = HesConnectionState.Disconnected;

            Mock<IHesAppConnection> hesAppConnectionMock = new Mock<IHesAppConnection>();
            hesAppConnectionMock.SetupGet(c => c.State).Returns(hesConnectionState);
            Mock<IWorkstationHelper> workstationHelperMock = new Mock<IWorkstationHelper>();
            workstationHelperMock.Setup(w => w.IsActiveSessionLocked()).Returns(false);

            Mock<IWorkstationUnlocker> workstationUnlockerMock = new Mock<IWorkstationUnlocker>();
            Mock<IScreenActivator> screenActivatorMock = new Mock<IScreenActivator>();
            Mock<IClientUiManager> uiMock = new Mock<IClientUiManager>();
            Mock<IHesAccessManager> hesAccesManagerMock = new Mock<IHesAccessManager>();
            Mock<ISettingsManager<ServiceSettings>> settingsManagerMock = new Mock<ISettingsManager<ServiceSettings>>();
            Mock<IMetaPubSub> messengerMock = new Mock<IMetaPubSub>();
            Mock<ILog> logMock = new Mock<ILog>();

            var controller = GetConnectionController(connectionId);

            Mock<IConnectionManager> connectionManagerMock = new Mock<IConnectionManager>();
            connectionManagerMock.SetupGet(m => m.Id).Returns((byte)DefaultConnectionIdProvider.WinBle);
            connectionManagerMock.Setup(m => m.Connect(connectionId)).Returns(Task.FromResult(controller))
                .Callback(()=> connectionManagerMock.Raise(m => m.ControllerAdded += null, new ControllerAddedEventArgs(controller)));
            connectionManagerMock.Setup(m => m.DeleteBond(controller))
                .Callback(() => connectionManagerMock.Raise(m => m.ControllerRemoved += null, new ControllerRemovedEventArgs(controller)));

            var coordinator = new ConnectionManagersCoordinator();
            coordinator.AddConnectionManager(connectionManagerMock.Object);

            int invokeCounter = 0;
            DeviceManager deviceManager = new DeviceManager(coordinator, logMock.Object);
            deviceManager.DeviceRemoved += (sender, e) => invokeCounter++;

            DeviceLogManager deviceLogManager = new DeviceLogManager(string.Empty, new Mock<IDeviceLogWriter>().Object, settingsManagerMock.Object, logMock.Object);

            EnterpriseConnectionFlowProcessor.ConnectionFlowSubprocessorsStruct connectionFlowSubprocessors = GetConnectionFlowSubprocessors(
                hesConnectionState,
                licenseCount,
                isMasterKeyRequired,
                isLinkRequired, 
                isLocked,
                deviceManager,
                controller);
            EnterpriseConnectionFlowProcessor connectionFlowProcessor = new EnterpriseConnectionFlowProcessor(deviceManager, hesAppConnectionMock.Object,
                workstationUnlockerMock.Object, screenActivatorMock.Object, uiMock.Object, hesAccesManagerMock.Object, settingsManagerMock.Object,
                connectionFlowSubprocessors, workstationHelperMock.Object, deviceLogManager, messengerMock.Object, logMock.Object);

            //Act
            await connectionFlowProcessor.Connect(connectionId);
            await (Task.Delay(1000));

            //Assert
            Assert.AreEqual(expectedResult, invokeCounter);
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
            Mock<IPermissionsCheckProcessor> permissionsCheckProcessorMock = new Mock<IPermissionsCheckProcessor>();
            Mock<ICacheVaultInfoProcessor> cacheVaultInfoProcessorMock = new Mock<ICacheVaultInfoProcessor>();
            Mock<IAccountsUpdateProcessor> accountsUpdateProcessorMock = new Mock<IAccountsUpdateProcessor>();
            Mock<IUnlockProcessor> unlockProcessorMock = new Mock<IUnlockProcessor>();
            Mock<IUserAuthorizationProcessor> userAuthorizationProcessorMock = new Mock<IUserAuthorizationProcessor>();

            var deviceMock = GetDeviceMock(connectionController);

            Mock<IVaultConnectionProcessor> vaultConnectionProcessorMock = new Mock<IVaultConnectionProcessor>();
            Task<IDevice> result = Task.FromResult(deviceMock.Object);
            vaultConnectionProcessorMock.Setup(p => p.ConnectVault(connectionController.Connection.ConnectionId, false, It.IsAny<CancellationToken>())).Returns(result).
                Callback(async () => await deviceManager.Connect(connectionController.Connection.ConnectionId));
            vaultConnectionProcessorMock.Setup(p => p.WaitVaultInitialization(deviceMock.Object, It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    deviceMock.SetupGet(d => d.LicenseInfo).Returns(licenseCount);
                    deviceMock.SetupGet(d => d.Id).Returns(deviceManager.Devices.FirstOrDefault().Id);
                    deviceMock.SetupGet(d => d.AccessLevel).Returns(new AccessLevel(isLinkRequired, false, isMasterKeyRequired, false, false, isLocked));
                    deviceMock.SetupGet(d => d.IsLocked).Returns(isLocked);
                });

            Mock<ILicensingProcessor> licensingProcessorMock = new Mock<ILicensingProcessor>();
            if (licenseCount == 0)
                licensingProcessorMock.Setup(p => p.CheckLicense(deviceMock.Object, It.IsAny<HwVaultInfoFromHesDto>(), It.IsAny<CancellationToken>()))
                    .Throws(new WorkflowException());

            Mock<IVaultAuthorizationProcessor> vaultAuthorizationProcessorMock = new Mock<IVaultAuthorizationProcessor>();
            Mock<IStateUpdateProcessor> stateUpdateProcessorMock = new Mock<IStateUpdateProcessor>();
            Mock<IActivationProcessor> activationProcessorMock = new Mock<IActivationProcessor>();
            if (hesConnectionState != HesConnectionState.Connected)
                if (isMasterKeyRequired)
                    vaultAuthorizationProcessorMock.Setup(p => p.AuthVault(deviceMock.Object, It.IsAny<CancellationToken>()))
                        .Throws(new WorkflowException());
                else
                if (isLinkRequired)
                    stateUpdateProcessorMock.Setup(p => p.UpdateVaultStatus(deviceMock.Object, It.IsAny<HwVaultInfoFromHesDto>(), It.IsAny<CancellationToken>()))
                        .Throws(new WorkflowException());
                else
                if (isLocked)
                    activationProcessorMock.Setup(p => p.ActivateVault(deviceMock.Object, It.IsAny<HwVaultInfoFromHesDto>(), It.IsAny<CancellationToken>()))
                        .Throws(new WorkflowException());
            return new EnterpriseConnectionFlowProcessor.ConnectionFlowSubprocessorsStruct()
            {
                AccountsUpdateProcessor = accountsUpdateProcessorMock.Object,
                ActivationProcessor = activationProcessorMock.Object,
                CacheVaultInfoProcessor = cacheVaultInfoProcessorMock.Object,
                LicensingProcessor = licensingProcessorMock.Object,
                MasterkeyProcessor = vaultAuthorizationProcessorMock.Object,
                PermissionsCheckProcessor = permissionsCheckProcessorMock.Object,
                StateUpdateProcessor = stateUpdateProcessorMock.Object,
                UnlockProcessor = unlockProcessorMock.Object,
                UserAuthorizationProcessor = userAuthorizationProcessorMock.Object,
                VaultConnectionProcessor = vaultConnectionProcessorMock.Object
            };
        }

        Mock<IDevice> GetDeviceMock(IConnectionController connectionController)
        {
            Mock<IDevice> deviceMock = new Mock<IDevice>();
            deviceMock.SetupGet(d => d.FirmwareVersion).Returns(new Version());
            deviceMock.SetupGet(d => d.Id).Returns(It.IsAny<string>());
            deviceMock.SetupGet(d => d.DeviceConnection).Returns(connectionController);

            return deviceMock;
        }

        IConnectionController GetConnectionController(ConnectionId connectionId)
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization() { ConfigureMembers = true });
            fixture.Inject(connectionId);
            fixture.Inject(ConnectionState.Connected);

            return fixture.Create<IConnectionController>();
        }
    }
}
