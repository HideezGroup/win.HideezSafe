using Hideez.ARM;
using Hideez.SDK.Communication.PasswordManager;
using HideezClient.Models;
using HideezClient.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezClient.Modules
{
    public static class AccountExtensions
    {
        public static Account[] FindAccountsByApp(this DeviceModel device, AppInfo appInfo)
        {
            List<Account> accounts = new List<Account>();

            foreach (var accountRecord in device.PasswordManager?.Accounts)
            {
                // login must be the same
                if (!string.IsNullOrEmpty(appInfo.Login))
                {
                    if (!appInfo.Login.Equals(accountRecord.Login, StringComparison.OrdinalIgnoreCase))
                        break;
                }

                if (MatchByDomain(appInfo, AccountUtility.Split(accountRecord.Urls))
                    || MatchByApp(appInfo, AccountUtility.Split(accountRecord.Apps)))
                {
                    accounts.Add(new Account(device, accountRecord));
                }
            }

            return accounts.ToArray();
        }

        // Marked as internal for unit tests
        internal static bool MatchByDomain(AppInfo appInfo, IEnumerable<string> domains)
        {
            if (!string.IsNullOrWhiteSpace(appInfo.Domain))
            {
                List<string> clearDomains = new List<string>();
                foreach (var domain in domains)
                {
                    if (domain.StartsWith("@"))
                        clearDomains.Add(domain.Substring(1));
                    else clearDomains.Add(domain);
                }

                return clearDomains.FirstOrDefault(d => appInfo.MatchesDomain(d)) != null;
            }
            return false;
        }

        internal static bool MatchByApp(AppInfo appInfo, IEnumerable<string> apps)
        {
            List<string> clearAppsNames = new List<string>();
            foreach (var app in apps)
            {
                if (app.StartsWith("@"))
                    clearAppsNames.Add(app.Substring(1));
                else clearAppsNames.Add(app);
            }

            foreach (var app in clearAppsNames)
            {
                if (!string.IsNullOrWhiteSpace(appInfo.Description))
                {
                    if (app.Contains('=')) // Description in format [App Name=bundle.id]
                    {
                        if (app.IndexOf(appInfo.Description, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    else
                    {
                        if (appInfo.Description.Equals(app, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(appInfo.ProcessName))
                {
                    if (appInfo.ProcessName.Equals(app, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
