using NUnit.Framework;
using System.Threading;

namespace ServiceLibrary.Implementation.Tests
{
    public class ServiceBuildDirectorTests
    {
        [SetUp]
        public void Init()
        {
        }

        [TearDown]
        public void Cleanup()
        {
            // Wait a bit to make sure that previous instance 
            // of service was fully shut down and cleaned 
            Thread.Sleep(1000);
        }

        [Test]
        public void Build_StandaloneConfiguration([Values] bool useDongle, [Values] bool useWinBle)
        {
            var featureList = new ToggleFeaturesList()
            {
                EnableDongleSupport = useDongle,
                EnableWinBleSupport = useWinBle,
                StartConnectionManagers = false,
            };

            var director = new HideezServiceBuildDirector();

            var builder = new HideezServiceBuilder();

            Assert.DoesNotThrow(() =>
            {
                director.BuildStandaloneService(builder, featureList);
            });
        }

        [Test]
        public void Build_EnterpriseConfiguration([Values] bool useDongle, [Values] bool useWinBle)
        {
            var featureList = new ToggleFeaturesList()
            {
                EnableDongleSupport = useDongle,
                EnableWinBleSupport = useWinBle,
                StartConnectionManagers = false,
            };

            var director = new HideezServiceBuildDirector();

            var builder = new HideezServiceBuilder();

            Assert.DoesNotThrow(() =>
            {
                director.BuildEnterpriseService(builder, featureList);
            });
        }
    }
}
