using HideezMiddleware.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezClient.Models.Settings
{
    [Serializable]
    public class IgnoredApplicationsSettings : BaseSettings
    {
        public IgnoredApplicationsSettings()
        {
            SettingsVersion = new Version(1, 1, 0);
            IgnoredProccesses = new IgnoredProcess[]
            {
                new IgnoredProcess("vmware", "VMWare Workstation"),
                new IgnoredProcess("VirtualBoxVM", "Oracle VM Virtual Box Manager"),
                new IgnoredProcess("mstsc", "Remote Desktop Connection")
            };
        }

        public IgnoredApplicationsSettings(IgnoredApplicationsSettings copy)
            : this()
        {
            if (copy == null)
                return;

            SettingsVersion = (Version)copy.SettingsVersion.Clone();
            IgnoredProccesses = copy.IgnoredProccesses.Select(h => h.DeepCopy()).ToArray();
        }

        [Setting]
        public Version SettingsVersion { get; }

        [Setting]
        public IgnoredProcess[] IgnoredProccesses { get; set; }
        public override object Clone()
        {
            return new IgnoredApplicationsSettings(this);
        }
    }
}
