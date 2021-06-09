namespace ServiceLibrary.Implementation
{
    /// <summary>
    /// This class is used to tell <see cref="HideezServiceBuildDirector"/>, which toggle-able features should be enabled.
    /// <para>
    /// Features toggled on by default:
    /// <list type="bullet">
    /// <item><see cref="EnableWinBleSupport"/> - Windows 10 BLE Support</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class ToggleFeaturesList
    {
        /// <summary>
        /// Enable Windows 10 BLE support.
        /// </summary>
        public bool EnableWinBleSupport { get; set; } = true;

        /// <summary>
        /// Enable Hideez Dongle Support.
        /// </summary>
        public bool EnableDongleSupport { get; set; } = false;

        /// <summary>
        /// Start connection managers and start watching for connections after service is built
        /// </summary>
        public bool StartConnectionManagers { get; set; } = true;
    }
}
