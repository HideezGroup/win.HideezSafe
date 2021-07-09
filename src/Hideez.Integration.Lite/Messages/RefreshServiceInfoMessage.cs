using Meta.Lib.Modules.PubSub;

namespace Hideez.Integration.Lite.Messages
{
    /// <summary>
    /// When sent to Hideez Service, immediately triggers broadcast 
    /// of <see cref="DevicesCollectionChangedMessage"/> message by service
    /// </summary>
    public sealed class RefreshServiceInfoMessage : PubSubMessageBase
    {
    }
}
