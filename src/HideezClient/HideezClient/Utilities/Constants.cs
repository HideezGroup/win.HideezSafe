using HideezMiddleware;
using HideezMiddleware.Utils.WorkstationHelper;
using System;
using System.Diagnostics;
using System.IO;

namespace HideezClient.Utilities
{
    static class Constants
    {
        public static string DefaultSettingsFolderPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
            "Hideez", 
            "Client",
            WorkstationInformationHelper.GetSessionName((uint)Process.GetCurrentProcess().SessionId),
            "Settings");

        public static string ApplicationSettingsFileName { get; } = "applicationsettings.xml";
        public static string HotkeySettingsFileName { get; } = "hotkeysettings.xml";
        public static string IgnoredApplicationSettingsFileName { get; } = "ignoredapplicationsettings.xml";
    }
}
