using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.FwUpdateCheck.Messages
{
    public class GetFwUpdateByDeviceModelMessage : PubSubMessageBase
    {
        public int ModelCode { get; }
        public GetFwUpdateByDeviceModelMessage(int modelCode)
        {
            ModelCode = modelCode;
        }
    }
}
