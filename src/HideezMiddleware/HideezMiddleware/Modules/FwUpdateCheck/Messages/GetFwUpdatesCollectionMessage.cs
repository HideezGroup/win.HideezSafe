using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.Modules.FwUpdateCheck.Messages
{
    public class GetFwUpdatesCollectionMessage: PubSubMessageBase
    {
        public int ModelCode { get; }

        public GetFwUpdatesCollectionMessage(int deviceModel)
        {
            ModelCode = deviceModel;
        }
    }
}
