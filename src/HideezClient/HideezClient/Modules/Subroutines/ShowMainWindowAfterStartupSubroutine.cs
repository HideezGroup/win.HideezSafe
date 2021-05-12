using Hideez.SDK.Communication.Log;
using HideezClient.Messages;
using Meta.Lib.Modules.PubSub;
using System.Threading.Tasks;

namespace HideezClient.Modules.Subroutines
{
    /// <summary>
    /// Wait for application startup to finish and then display main app window
    /// </summary>
    internal sealed class ShowMainWindowAfterStartupSubroutine : SubroutineBase
    {
        public ShowMainWindowAfterStartupSubroutine(IMetaPubSub messenger, ILog log) 
            : base(messenger, nameof(ShowMainWindowAfterStartupSubroutine), log)
        {
            _messenger.Subscribe<ApplicationStartupFinishedMessage>(OnApplicationStartupFinished);
            _ = SafePublish(new SubroutineCreated(this));
        }

        private Task OnApplicationStartupFinished(ApplicationStartupFinishedMessage arg)
        {
            Task.Run(async () =>
            {
                await _messenger.Unsubscribe<ApplicationStartupFinishedMessage>(OnApplicationStartupFinished);
                await SafePublish(new ShowActivateMainWindowMessage());
                await SafePublish(new SubroutineFinished(this));
            });

            return Task.CompletedTask;
        }
    }
}
