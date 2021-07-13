using AutoFixture;
using Hideez.SDK.Communication;
using HideezMiddleware.Settings;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Tests
{
    class SdkConfigLoaderTests
    {
        /// <summary>
        /// Test that checks that all SdkConfig properties are present in SdkSettings
        /// </summary>
        [Test]
        public void SdkConfig_PropertiesMatch_SdkSettings()
        {
            // Arrange
            var configProperties = typeof(SdkConfig).GetProperties();
            var settingsProperties = typeof(SdkSettings).GetProperties();

            // Act
            var missingProperties = new List<PropertyInfo>();
            foreach (var cProp in configProperties)
            {
                if (settingsProperties
                    .FirstOrDefault(sProp => sProp.Name == cProp.Name
                    && sProp.PropertyType == cProp.PropertyType) == null)
                    missingProperties.Add(cProp);
            }


            // Assert
            Assert.IsEmpty(missingProperties);
        }

        [Test]
        public void SdkConfigLoader_LoadSdkSettings_SdkConfigChanged()
        {
            // TODO: Currently impossible to properly test due to 
            // the nature of SdkConfig being a static class
            
            // Arrange

            // Act

            //Assert

        }
    }
}
