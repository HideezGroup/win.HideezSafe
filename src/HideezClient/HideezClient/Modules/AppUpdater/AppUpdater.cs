using Hideez.SDK.Communication.Log;
using HideezClient.Messages;
using HideezMiddleware.Localize;
using HideezMiddleware.Modules.UpdateCheck.Messages;
using Meta.Lib.Modules.PubSub;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HideezClient.Modules
{
    internal sealed class AppUpdater : Logger, IAppUpdater
    {
        readonly IMetaPubSub _messenger;
        readonly IWindowsManager _windowsManager;
        string updateFilepath = string.Empty;
        int _interlockedUpdate = 0;

        public AppUpdater(IWindowsManager windowsManager, IMetaPubSub messenger, ILog log)
            : base(nameof(AppUpdater), log)
        {
            _messenger = messenger;
            _windowsManager = windowsManager;

            _messenger.TrySubscribeOnServer<ApplicationUpdateAvailableMessage>(OnUpdateAvailable);
            _messenger.Subscribe<StartApplicationUpdateMessage>(OnStartUpdage);
        }

        private Task OnUpdateAvailable(ApplicationUpdateAvailableMessage msg)
        {
            updateFilepath = msg.Filepath;

            Task.Run(async () =>
            {
                try
                {
                    var title = TranslationSource.Instance["UpdateAvailableNotification.Title"];
                    var message = string.Format(TranslationSource.Instance["UpdateAvailableNotification.Message"], msg.Version);
                    var updateNow = await _windowsManager.ShowUpdateAvailableNotification(title, message);
                    if (updateNow)
                        await StartUpdate();
                }
                catch (Exception)
                {
                }
            });

            return Task.CompletedTask;
            
        }

        private Task OnStartUpdage(StartApplicationUpdateMessage msg)
        {
            Task.Run(StartUpdate);
            return Task.CompletedTask;
        }

        private Task StartUpdate()
        {
            if (Interlocked.CompareExchange(ref _interlockedUpdate, 1, 0) == 0)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(updateFilepath))
                        return Task.CompletedTask;

                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = updateFilepath,
                        Verb = "runas",
                        UseShellExecute = true,
                        Arguments = "-passive",
                    };
                    Process.Start(processStartInfo);
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _interlockedUpdate, 0);
                }
            }

            return Task.CompletedTask;
        }
    }
}
