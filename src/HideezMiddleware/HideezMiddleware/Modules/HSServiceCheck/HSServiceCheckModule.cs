using Hideez.SDK.Communication.Log;
using Meta.Lib.Modules.PubSub;
using System;
using System.ServiceProcess;

namespace HideezMiddleware.Modules.HSServiceCheck
{
    public class HSServiceCheckModule : ModuleBase
    {
        public HSServiceCheckModule(IMetaPubSub messenger, ILog log)
            : base(messenger, nameof(HSServiceCheckModule), log)
        {
            try
            {
                ServiceController sc = new ServiceController("HideezServer");

                switch (sc.Status)
                {
                    case ServiceControllerStatus.Running:
                        WriteLine("Detected running Hideez Safe service. This may cause unexpected behavior", LogErrorSeverity.Warning);
                        break;
                    case ServiceControllerStatus.StopPending:
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.Stopped:
                    case ServiceControllerStatus.Paused:
                    default:
                        return;
                }
            }
            catch (Exception)
            {
                // Silent handling
            }
        }
    }
}
