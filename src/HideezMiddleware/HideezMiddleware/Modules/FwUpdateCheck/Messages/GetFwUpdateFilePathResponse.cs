using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.FwUpdateCheck.Messages
{
    public class GetFwUpdateFilePathResponse : PubSubMessageBase
    {
        public string FilePath { get; }

        public GetFwUpdateFilePathResponse(string filePath)
        {
            FilePath = filePath;
        }
    }
}
