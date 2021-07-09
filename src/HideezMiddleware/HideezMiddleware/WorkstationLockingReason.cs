namespace HideezMiddleware
{
    public enum WorkstationLockingReason
    {
        NonHideez, // Obsolete
        DeviceBelowThreshold,
        DeviceConnectionLost,
        ProximityTimeout,
        ThirdParty,
    }
}
