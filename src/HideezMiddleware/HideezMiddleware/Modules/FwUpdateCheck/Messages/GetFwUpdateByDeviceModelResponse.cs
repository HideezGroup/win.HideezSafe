using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.FwUpdateCheck.Messages
{
    public class GetFwUpdateByDeviceModelResponse: PubSubMessageBase
    {
        public string FilePath { get; }

        public GetFwUpdateByDeviceModelResponse(string filePath)
        {
            FilePath = filePath;
        }
    }
}
