using Hideez.SDK.Communication.Log;
using HideezMiddleware.Utils;
using Meta.Lib.Modules.PubSub;
using System;

namespace HideezClient.Modules.Subroutines
{
    internal abstract class SubroutineBase : SafeMessengerBase
    {
        public string Id { get; }

        public SubroutineBase(IMetaPubSub messenger, string source, ILog log)
            : base(messenger, source, log)
        {
            Id = Guid.NewGuid().ToString();
        }
    }
}
