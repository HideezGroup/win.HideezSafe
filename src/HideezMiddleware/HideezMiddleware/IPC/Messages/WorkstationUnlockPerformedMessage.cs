using Hideez.SDK.Communication;
using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.IPC.Messages
{
    internal sealed class WorkstationUnlockPerformedMessage : PubSubMessageBase
    {

        /// <summary>
        /// Id of the connection flow that performed workstation unlock
        /// </summary>
        public string FlowId { get; set; } = string.Empty;


        /// <summary>
        /// Tells if workstation unlock was successful
        /// </summary>
        public bool IsSuccessful { get; set; } = false;

        /// <summary>
        /// Method that was used to perform workstation unlock
        /// </summary>
        public SessionSwitchSubject UnlockMethod { get; set; } = SessionSwitchSubject.Manual;


        /// <summary>
        /// Name of account used for workstation unlock
        /// </summary>
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Login used for workstation unlock
        /// </summary>
        public string AccountLogin { get; set; } = string.Empty;

        /// <summary>
        /// Mac of the device that was used for unlock
        /// </summary>
        public string Mac { get; set; } = string.Empty;

        public WorkstationUnlockPerformedMessage(string flowId, bool isSuccessful, SessionSwitchSubject unlockMethod,  string accountName, string accountLogin, string mac)
        {
            FlowId = flowId;
            IsSuccessful = isSuccessful;
            UnlockMethod = unlockMethod;
            AccountName = accountName;
            AccountLogin = accountLogin;
            Mac = mac;
        }

    }
}
