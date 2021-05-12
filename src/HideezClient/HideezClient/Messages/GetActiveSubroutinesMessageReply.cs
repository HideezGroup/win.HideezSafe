using HideezClient.Modules.Subroutines;
using Meta.Lib.Modules.PubSub;
using System.Collections.Generic;

namespace HideezClient.Messages
{
    internal sealed class GetActiveSubroutinesMessageReply : PubSubMessageBase
    {
        public IEnumerable<SubroutineBase> ActiveSubroutines { get; set; }

        public GetActiveSubroutinesMessageReply(IEnumerable<SubroutineBase> activeSubroutines)
        {
            ActiveSubroutines = activeSubroutines;
        }
    }
}
