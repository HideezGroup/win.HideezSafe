using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.FwUpdateCheck
{
    public class DeviceModelInfo
    {
        public string Name { get; }
        public int Code { get; }

        public DeviceModelInfo(string name, int code)
        {
            Name = name;
            Code = code;
        }
    }
}
