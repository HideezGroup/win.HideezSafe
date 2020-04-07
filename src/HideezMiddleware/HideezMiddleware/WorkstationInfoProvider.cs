﻿using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Utils;
using Hideez.SDK.Communication.Workstation;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HideezMiddleware
{
    public class WorkstationInfoProvider : Logger, IWorkstationInfoProvider
    {
        public WorkstationInfoProvider(ILog log)
            : base(nameof(WorkstationInfoProvider), log)
        {
        }

        public string WorkstationId
        {
            get
            {
                return Environment.MachineName;
            }
        }

        public WorkstationInfo GetWorkstationInfo()
        {
            WorkstationInfo workstationInfo = new WorkstationInfo();

            try
            {
                workstationInfo.AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                workstationInfo.MachineName = Environment.MachineName;
                workstationInfo.Domain = Environment.UserDomainName;

                try
                {
                    RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion");
                    workstationInfo.OsName = (string)registryKey.GetValue("ProductName");
                    workstationInfo.OsVersion = (string)registryKey.GetValue("ReleaseId");
                    workstationInfo.OsBuild = $"{registryKey.GetValue("CurrentBuild")}.{registryKey.GetValue("UBR")}";
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                    Debug.Assert(false, "An exception occured while querrying workstation operating system");
                }

                workstationInfo.Users = WorkstationHelper.GetAllUserNames();
            }
            catch (Exception ex)
            {
                WriteLine(ex);
                Debug.Assert(false);
            }

            return workstationInfo;
        }
    }
}
