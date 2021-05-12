using HideezClient.Modules.Subroutines;
using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages
{
    internal sealed class SubroutineCreated : PubSubMessageBase
    {
        public SubroutineBase Subroutine { get; set; }

        public SubroutineCreated(SubroutineBase subroutine)
        {
            Subroutine = subroutine;
        }
    }
}
