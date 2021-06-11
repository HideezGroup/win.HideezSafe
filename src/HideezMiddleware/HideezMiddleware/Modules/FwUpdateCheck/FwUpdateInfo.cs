using System;

namespace HideezMiddleware.Modules.FwUpdateCheck
{
    public enum ReleaseStage
    {
        Alpha,
        Beta, 
        Release
    }

    public class FwUpdateInfo: IComparable<FwUpdateInfo>
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public int ModelCode { get; set; }
        public ReleaseStage ReleaseStage { get; set; }

        public int CompareTo(FwUpdateInfo other)
        {
            return new Version(Version).CompareTo(new Version(other.Version));
        }
    }
}
