using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezMiddleware.Modules.FwUpdateCheck
{
    public enum ReleaseStage
    {
        Alpha,
        Beta, 
        Release
    }

    public class FwUpdateInfo
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public int ModelCode { get; set; }
        public ReleaseStage ReleaseStage { get; set; }
    }
}
