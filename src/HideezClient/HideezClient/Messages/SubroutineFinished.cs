using HideezClient.Modules.Subroutines;
using Meta.Lib.Modules.PubSub;

namespace HideezClient.Messages
{
    internal sealed class SubroutineFinished : PubSubMessageBase
    {
        public SubroutineBase Subroutine { get; set; }

        public SubroutineFinished(SubroutineBase subroutine)
        {
            Subroutine = subroutine;
        }
    }
}
