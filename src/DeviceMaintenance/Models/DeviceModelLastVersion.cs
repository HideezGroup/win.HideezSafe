using HideezMiddleware.Localize;
using HideezMiddleware.Modules.FwUpdateCheck;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceMaintenance.Models
{
    public class DeviceModelLastVersion
    {
        public DeviceModelInfo DeviceModel { get; }
        public string DeviceModelName { get => TranslationSource.Instance[DeviceModel.Name]; }
        public FwUpdateInfo FwUpdateInfo { get; }

        public DeviceModelLastVersion(DeviceModelInfo deviceModel, FwUpdateInfo fwUpdateInfo)
        {
            DeviceModel = deviceModel;
            FwUpdateInfo = fwUpdateInfo;
        }
    }
}
