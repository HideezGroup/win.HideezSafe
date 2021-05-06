using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.FwUpdateCheck.Messages
{
    public class GetFwUpdatesCollectionResponse: PubSubMessageBase
    {
        public FwUpdateInfo[] FwUpdatesInfo { get; }

        public GetFwUpdatesCollectionResponse(FwUpdateInfo[] fwUpdatesInfo)
        {
            FwUpdatesInfo = fwUpdatesInfo;
        }
    }
}
