namespace Hideez.Integration.Lite.Enums
{
    /// <summary>
    /// This is a copy of Hideez.SDK.Communication.HES.DTO
    /// You can use either one when parsing values received from Hideez Service
    /// </summary>
    public enum HwVaultConnectionState
    {
        /// <summary>
        /// Default state.
        /// </summary>
        Offline = 0,
        /// <summary>
        /// MainWorkflow is running. Only client can initiates Remote Connections and Remote Tasks.
        /// </summary>
        Initializing = 1,
        /// <summary>
        /// MainWorkflow is finishing. Workstation should be unlocked by now. Vault now may cause workstation lock
        /// </summary>
        Finalizing = 2,
        /// <summary>
        /// MainWorkflow finished. HES execute RemoteConnections and Remote Tasks when it wants.
        /// </summary>
        Online = 3
    }
}
