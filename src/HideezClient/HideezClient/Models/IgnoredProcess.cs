using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezClient.Models
{
    [Serializable]
    public class IgnoredProcess
    {
        public string Process { get; set; }

        public string ProcessName { get; set; }

        public IgnoredProcess()
        {
            Process = string.Empty;
            ProcessName = string.Empty;
        }

        public IgnoredProcess(string process, string processName)
        {
            Process = process;
            ProcessName = processName;
        }
        public IgnoredProcess DeepCopy()
        {
            return new IgnoredProcess(Process, ProcessName);
        }
    }
}
