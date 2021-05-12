using Hideez.SDK.Communication.Log;
using HideezClient.Messages;
using HideezMiddleware.Utils;
using Meta.Lib.Modules.PubSub;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HideezClient.Modules.Subroutines
{
    internal sealed class SubroutineContainer : SafeMessengerBase
    {
        readonly Dictionary<string, SubroutineBase> _activeSubroutines = new Dictionary<string, SubroutineBase>();

        public SubroutineContainer(IMetaPubSub messenger, ILog log)
            : base(messenger, nameof(SubroutineContainer), log)
        {
            _messenger.Subscribe(GetSafeHandler<SubroutineCreated>(OnSubroutineCreated));
            _messenger.Subscribe(GetSafeHandler<SubroutineFinished>(OnSubroutineFinished));
            _messenger.Subscribe(GetSafeHandler<GetActiveSubroutinesMessage>(OnGetActiveSubroutines));
        }

        Task OnSubroutineCreated(SubroutineCreated msg)
        {
            _activeSubroutines[msg.Subroutine.Id] = msg.Subroutine;
            return Task.CompletedTask;
        }

        Task OnSubroutineFinished(SubroutineFinished msg)
        {
            _activeSubroutines.Remove(msg.Subroutine.Id);
            return Task.CompletedTask;
        }

        async Task OnGetActiveSubroutines(GetActiveSubroutinesMessage msg)
        {
            var subroutinesList = new List<SubroutineBase>(_activeSubroutines.Values);
            await SafePublish(new GetActiveSubroutinesMessageReply(subroutinesList));
        }
    }
}
