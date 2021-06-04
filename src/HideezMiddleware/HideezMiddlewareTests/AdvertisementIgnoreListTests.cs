using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Proximity.Interfaces;
using HideezMiddleware.DeviceConnection;
using HideezMiddleware.Settings;
using Moq;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.Tests
{
    [Parallelizable(ParallelScope.All)]
    public class AdvertisementIgnoreListTests
    {
        readonly int _rssiDelaySeconds = 4;
        readonly int _ignoreLifetime = 2;
        readonly int _lockProximity = 30;

        [Test]
        public void IgnoredId_CheckIfIgnored_Ignored()
        {
            // Arrange
            var connectionManagerMock = new Mock<IBleConnectionManager>();

            var proximitySettingsManager = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsManager.Setup(mock => mock.GetLockProximity(It.IsAny<ConnectionId>())).Returns(_lockProximity);

            var logMock = new Mock<ILog>();

            var advIgnoreList = new AdvertisementIgnoreList(connectionManagerMock.Object, proximitySettingsManager.Object, _rssiDelaySeconds, logMock.Object);

            var id = Guid.NewGuid().ToString();

            // Act
            advIgnoreList.Ignore(id);

            // Assert
            Assert.IsTrue(advIgnoreList.IsIgnored(id));
        }

        [Test]
        public void IgnoredIdForTime_CheckIfIgnored_Ignored()
        {
            // Arrange
            var connectionManagerMock = new Mock<IBleConnectionManager>();

            var proximitySettingsManager = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsManager.Setup(mock => mock.GetLockProximity(It.IsAny<ConnectionId>())).Returns(_lockProximity);

            var logMock = new Mock<ILog>();

            var advIgnoreList = new AdvertisementIgnoreList(connectionManagerMock.Object, proximitySettingsManager.Object, _rssiDelaySeconds, logMock.Object);

            var id = Guid.NewGuid().ToString();

            // Act
            advIgnoreList.IgnoreForTime(id, _ignoreLifetime);

            // Assert
            Assert.IsTrue(advIgnoreList.IsIgnored(id));
        }

        [Test]
        public void IgnoredId_WaitTimeoutAndCheckIfIgnored_NotIgnored()
        {
            // Arrange
            var connectionManagerMock = new Mock<IBleConnectionManager>();

            var proximitySettingsManager = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsManager.Setup(mock => mock.GetLockProximity(It.IsAny<ConnectionId>())).Returns(_lockProximity);

            var logMock = new Mock<ILog>();

            var advIgnoreList = new AdvertisementIgnoreList(connectionManagerMock.Object, proximitySettingsManager.Object, _rssiDelaySeconds, logMock.Object);

            var id = Guid.NewGuid().ToString();

            // Act
            advIgnoreList.Ignore(id);

            // Assert
            Assert.That(() => advIgnoreList.IsIgnored(id), Is.False.After(_rssiDelaySeconds).Seconds);
        }

        [Test]
        public void IgnoredIdForTime_WaitTimeoutAndCheckIfIgnored_NotIgnored()
        {
            // Arrange
            var connectionManagerMock = new Mock<IBleConnectionManager>();

            var proximitySettingsManager = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsManager.Setup(mock => mock.GetLockProximity(It.IsAny<ConnectionId>())).Returns(_lockProximity);

            var logMock = new Mock<ILog>();

            var advIgnoreList = new AdvertisementIgnoreList(connectionManagerMock.Object, proximitySettingsManager.Object, _rssiDelaySeconds, logMock.Object);

            var id = Guid.NewGuid().ToString();

            // Act
            advIgnoreList.IgnoreForTime(id, _ignoreLifetime);

            // Assert
            Assert.That(() => advIgnoreList.IsIgnored(id), Is.False.After(_ignoreLifetime).Seconds);
        }

        [Test]
        public void IgnoredId_SentAdvertisementsAboveLockAndWaitTimeout_Ignored()
        {
            // Arrange
            var connectionManagerMock = new Mock<IBleConnectionManager>();

            var proximitySettingsManager = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsManager.Setup(mock => mock.GetLockProximity(It.IsAny<ConnectionId>())).Returns(_lockProximity);

            var logMock = new Mock<ILog>();

            var advIgnoreList = new AdvertisementIgnoreList(connectionManagerMock.Object, proximitySettingsManager.Object, _rssiDelaySeconds, logMock.Object);

            var id = Guid.NewGuid().ToString();

            var lockRssi = (sbyte)BleUtils.ProximityToRssi(_lockProximity + 2);

            // Act
            advIgnoreList.Ignore(id);

            Task.Run(async () =>
            {
                await Task.Delay(_rssiDelaySeconds/2 * 1000);
                connectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, new AdvertismentReceivedEventArgs(string.Empty, id, lockRssi));
            });

            // Assert
            Assert.That(() => advIgnoreList.IsIgnored(id), Is.True.After(_rssiDelaySeconds).Seconds);
        }

        [Test]
        public void IgnoredIdForTime_SentAdvertisementsAboveLockAndWaitTimeout_NotIgnored()
        {
            // Arrange
            var connectionManagerMock = new Mock<IBleConnectionManager>();

            var proximitySettingsManager = new Mock<IDeviceProximitySettingsProvider>();
            proximitySettingsManager.Setup(mock => mock.GetLockProximity(It.IsAny<ConnectionId>())).Returns(_lockProximity);

            var logMock = new Mock<ILog>();

            var advIgnoreList = new AdvertisementIgnoreList(connectionManagerMock.Object, proximitySettingsManager.Object, _rssiDelaySeconds, logMock.Object);

            var id = Guid.NewGuid().ToString();

            var lockRssi = (sbyte)BleUtils.ProximityToRssi(_lockProximity + 2);

            // Act
            advIgnoreList.IgnoreForTime(id, _ignoreLifetime);

            Task.Run(async () =>
            {
                await Task.Delay(_ignoreLifetime / 2 * 1000);
                connectionManagerMock.Raise(mock => mock.AdvertismentReceived += null, new AdvertismentReceivedEventArgs(string.Empty, id, lockRssi));
            });

            // Assert
            Assert.That(() => advIgnoreList.IsIgnored(id), Is.False.After(_ignoreLifetime).Seconds);
        }
    }
}
