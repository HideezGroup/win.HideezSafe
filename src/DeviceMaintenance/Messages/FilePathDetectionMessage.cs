using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceMaintenance.Messages
{
    class FilePathDetectionMessage: PubSubMessageBase
    {
        public string DeviceSerialNo { get; set; }

        public FilePathDetectionMessage(string serialNo)
        {
            DeviceSerialNo = serialNo;
        }
    }
}
