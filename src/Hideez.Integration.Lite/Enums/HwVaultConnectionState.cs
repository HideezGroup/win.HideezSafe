namespace Hideez.Integration.Lite.Enums
{
    public enum HwVaultConnectionState
    {
        /// <summary>
        /// Default state.
        /// </summary>
        Offline,
        /// <summary>
        /// MainWorkflow is running. Only client can initiates Remote Connections and Remote Tasks.
        /// </summary>
        Initializing,
        /// <summary>
        /// MainWorkflow is finishing. Workstation should be unlocked by now. Vault now may cause workstation lock
        /// </summary>
        Finalizing,
        /// <summary>
        /// MainWorkflow finished. HES execute RemoteConnections and Remote Tasks when it wants.
        /// </summary>
        Online
    }
}
