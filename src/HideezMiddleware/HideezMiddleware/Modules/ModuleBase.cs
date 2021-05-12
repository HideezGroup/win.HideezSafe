using Hideez.SDK.Communication.Log;
using HideezMiddleware.Utils;
using Meta.Lib.Modules.PubSub;

namespace HideezMiddleware.Modules
{
    public abstract class ModuleBase : SafeMessengerBase, IModule
    {

        protected ModuleBase(IMetaPubSub messenger, string source, ILog log) 
            : base(messenger, source, log)
        {
        }
    }
}
