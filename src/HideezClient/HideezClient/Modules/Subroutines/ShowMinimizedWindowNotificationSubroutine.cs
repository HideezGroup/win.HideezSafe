using Hideez.SDK.Communication.Log;
using HideezClient.Messages;
using Meta.Lib.Modules.PubSub;
using System.Threading.Tasks;

namespace HideezClient.Modules.Subroutines
{
    /// <summary>
    /// Wait for the main window to be closed and then ask once to display 
    /// notification that explains that its actually minimized
    /// </summary>
    internal sealed class ShowMinimizedWindowNotificationSubroutine : SubroutineBase
    {
        public ShowMinimizedWindowNotificationSubroutine(IMetaPubSub messenger, ILog log)
            : base(messenger, nameof(ShowMinimizedWindowNotificationSubroutine), log)
        {
            _messenger.Subscribe<MainWindowClosedMessage>(OnMainWindowClosed);
            _ = SafePublish(new SubroutineCreated(this));
        }

        private Task OnMainWindowClosed(MainWindowClosedMessage arg)
        {
            Task.Run(async () => 
            {
                await Task.Delay(200); // Small delay to avoid overlap with window closing anim
                await _messenger.Unsubscribe<MainWindowClosedMessage>(OnMainWindowClosed);
                await SafePublish(new ShowMinimizedWindowHelpNotificationMessage());
                await SafePublish(new SubroutineFinished(this));
            });

            return Task.CompletedTask;
        }
    }
}
