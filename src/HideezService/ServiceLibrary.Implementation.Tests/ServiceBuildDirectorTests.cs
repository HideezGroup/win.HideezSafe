using NUnit.Framework;
using System.Threading;

namespace ServiceLibrary.Implementation.Tests
{
    public class ServiceBuildDirectorTests
    {
        [SetUp]
        public void Init()
        {
            Thread.Sleep(50);
        }

        [TearDown]
        public void Cleanup()
        {
            Thread.Sleep(50);
        }

        [Test]
        public void Build_StandaloneConfiguration([Values] bool useDongle, [Values] bool useWinBle)
        {
            var featureList = new ToggleFeaturesList()
            {
                EnableDongleSupport = useDongle,
                EnableWinBleSupport = useWinBle,
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
