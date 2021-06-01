using GalaSoft.MvvmLight.Messaging;
using Hideez.SDK.Communication;
using HideezClient.Messages;
using HideezClient.Models;
using Meta.Lib.Modules.PubSub;
using System.Threading.Tasks;

namespace HideezClient.Modules.ButtonManager
{
    internal sealed class ButtonManager : IButtonManager
    {
        readonly IMetaPubSub _metaMessenger;
        bool _enabled;

        public ButtonManager(IMetaPubSub metaMessenger)
        {
            _metaMessenger = metaMessenger;
        }

        public bool Enabled
        {
            get
            {
                return _enabled;
            }
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnManagerEnabledChanged(value);
                }
            }
        }

        void OnManagerEnabledChanged(bool newValue)
        {
            Task.Run(() =>
            {
                if (newValue)
                {
                    _metaMessenger.Subscribe<DeviceButtonPressedMessage>(OnDeviceButtonPressed);
                }
                else
                {
                    _metaMessenger.Unsubscribe<DeviceButtonPressedMessage>(OnDeviceButtonPressed);
                }
            });
        }

        async Task OnDeviceButtonPressed(DeviceButtonPressedMessage msg)
        {
            UserAction action;
            switch (msg.Button)
            {
                case ButtonPressCode.Double:
                    action = UserAction.InputLogin;
                    break;
                case ButtonPressCode.Triple:
                    action = UserAction.InputPassword;
                    break;
                case ButtonPressCode.Quad:
                    action = UserAction.InputOtp;
                    break;
                default:
                    return;
            }

            await _metaMessenger.Publish(new ButtonPressedMessage(msg.DeviceId, action, msg.Button));
        }
    }
}
