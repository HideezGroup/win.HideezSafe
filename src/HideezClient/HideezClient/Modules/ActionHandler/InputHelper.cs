using Hideez.ARM;
using Hideez.ISM;
using System;

namespace HideezClient.Modules.ActionHandler
{
    class InputHelper
    {
        public static InputApp GetInputApp(AppInfo appInfo)
        {
            // ToDo: Refactor the way check for special cases is performed in ActionHandl
            InputApp inputApp = InputApp.Normal;

            // Special case for the Microsoft Edge
            if ((appInfo.ProcessName.StartsWith("applicationframehost", StringComparison.CurrentCultureIgnoreCase) &&
                (!string.IsNullOrWhiteSpace(appInfo.Domain) || appInfo.Title.StartsWith("skype", StringComparison.CurrentCultureIgnoreCase)))
                || appInfo.ProcessName.StartsWith("microsoftedge", StringComparison.CurrentCultureIgnoreCase))
            {
                inputApp = InputApp.Edge;
            }
            else if (appInfo.Title.StartsWith("skype", StringComparison.CurrentCultureIgnoreCase))
            {
                inputApp = InputApp.Skype;
            }
            else if (appInfo.ProcessName.StartsWith("ezvit", StringComparison.CurrentCultureIgnoreCase))
            {
                inputApp = InputApp.Medoc;
            }

            return inputApp;
        }

        public static bool IsBrowser(AppInfo appInfo)
        {
            // Only appinfo provided for browser window would contain domain
            return !string.IsNullOrWhiteSpace(appInfo.Domain);
        }
    }
}
