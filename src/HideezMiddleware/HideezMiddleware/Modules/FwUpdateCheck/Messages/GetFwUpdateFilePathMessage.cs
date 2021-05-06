using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.Modules.FwUpdateCheck.Messages
{
    public class GetFwUpdateFilePathMessage : PubSubMessageBase
    {
        public FwUpdateInfo FwUpdateInfo { get; set; }

        public GetFwUpdateFilePathMessage(FwUpdateInfo fwUpdateInfo)
        {
            FwUpdateInfo = fwUpdateInfo;
        }
    }
}
