using HideezMiddleware.Localize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DeviceMaintenance.Localize
{
    /// <summary>
    /// Binding localization key to resource localized.
    /// </summary>
    public class LocalizationExtension : Binding
    {
        public LocalizationExtension(string key) : base("[" + key + "]")
        {
            this.Mode = BindingMode.OneWay;
            this.Source = TranslationSource.Instance;
        }
    }
}
