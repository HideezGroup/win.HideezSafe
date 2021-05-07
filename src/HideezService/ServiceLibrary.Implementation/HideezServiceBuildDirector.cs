using System;

namespace ServiceLibrary.Implementation
{
    public sealed class HideezServiceBuildDirector
    {
        public HideezService BuildEnterpriseService(HideezServiceBuilder builder, ToggleFeaturesList featuresList)
        {
            builder.Begin();

            builder.AddFatalExceptionHandling();
            builder.AddEnterpriseResources();
            builder.AddHSServiceCheck();
            builder.AddEnterpriseProximitySettingsSupport();
            builder.AddHES();
            builder.AddEnterpriseConnectionFlow();
            if (featuresList.EnableDongleSupport)
                builder.AddDongleSupport();
            if (featuresList.EnableWinBleSupport)
                builder.AddWinBleSupport();
            builder.AddRfidSupport();
            builder.AddRemoteUnlock();
            builder.AddWorkstationLock();
            builder.AddClientPipe();
            builder.AddAudit();
            builder.End();

            return builder.GetService();
        }

        public HideezService BuildStandaloneService(HideezServiceBuilder builder, ToggleFeaturesList featuresList)
        {
            builder.Begin();

            builder.AddFatalExceptionHandling();
            builder.AddStandaloneResources();
            builder.AddHSServiceCheck();
            builder.AddStandaloneProximitySettingsSupport();
            builder.AddStandaloneConnectionFlow();
            if (featuresList.EnableDongleSupport)
                builder.AddDongleSupport();
            if (featuresList.EnableWinBleSupport)
                builder.AddWinBleSupport();
            builder.AddRemoteUnlock();
            builder.AddWorkstationLock();
            builder.AddClientPipe();
            builder.AddUpdateCheck();
            builder.End();

            return builder.GetService();
        }
    }
}
