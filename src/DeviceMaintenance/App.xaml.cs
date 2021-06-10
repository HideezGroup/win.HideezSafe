using DeviceMaintenance.Resources;
using HideezMiddleware.Localize;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DeviceMaintenance
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            //localization
            var currentCulture = CultureInfo.InstalledUICulture;
            var uaCulture = new CultureInfo("uk-UA");
            var specifiedCulture = new CultureInfo("en-US");

            if (currentCulture.Equals(new CultureInfo("ru-RU")) || currentCulture.Equals(uaCulture))
            {
                specifiedCulture = uaCulture;
            }

            TranslationSource.Instance.CurrentCulture = specifiedCulture;
            Thread.CurrentThread.CurrentCulture = specifiedCulture;
            Thread.CurrentThread.CurrentUICulture = specifiedCulture;

            ResourceManagersProvider.SetResources(typeof(Strings));
        }
    }
}
