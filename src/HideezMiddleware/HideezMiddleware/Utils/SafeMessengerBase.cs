using Hideez.SDK.Communication.Log;
using Meta.Lib.Modules.PubSub;
using System;
using System.Threading.Tasks;

namespace HideezMiddleware.Utils
{
    public abstract class SafeMessengerBase : Logger
    {
        readonly protected IMetaPubSub _messenger;

        public SafeMessengerBase(IMetaPubSub messenger, string source, ILog log)
            : base(source, log)
        {
            _messenger = messenger;
        }

        /// <summary>
        /// Wraps message publishing into a try-catch block 
        /// </summary>
        protected async Task SafePublish(IPubSubMessage message, bool logError = false)
        {
            try
            {
                await _messenger.Publish(message);
            }
            catch (Exception ex)
            {
                if (logError)
                    WriteLine(ex);
            }
        }

        /// <summary>
        /// Wraps message handling delegate into a try-catch block
        /// </summary>
        protected Func<T, Task> GetSafeHandler<T>(Func<T, Task> handler)
        {
            return new Func<T, Task>(async (arg) =>
            {
                try
                {
                    await handler.Invoke(arg);
                }
                catch (Exception ex)
                {
                    WriteLine(ex);
                }
            });
        }
    }
}
