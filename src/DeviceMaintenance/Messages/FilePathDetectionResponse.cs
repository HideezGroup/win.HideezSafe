using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceMaintenance.Messages
{
    class FilePathDetectionResponse : PubSubMessageBase
    {
        public string FilePath { get; set; }

        public FilePathDetectionResponse(string filePath)
        {
            FilePath = filePath;
        }
    }
}
