using HideezClient.Messages.Hotkeys;
using Meta.Lib.Modules.PubSub;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HideezClient.Modules.HotkeyManager
{
    /// <summary>
    /// Responsible for tracking and remembering hotkeys registration state
    /// <para>
    /// Subscribed messages
    /// <list type="bullet">
    /// <item><see cref="SettingsChangedMessage<HotkeySettings>"/></item>
    /// <item><see cref="ResetHotkeysMessage"/></item>
    /// <item><see cref="EnableHotkeyManagerMessage"/></item>
    /// <item><see cref="DisableHotkeyManagerMessage"/></item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class HotkeyStatesMonitor : IHotkeyStatesMonitor
    {
        readonly IMetaPubSub _messenger;

        readonly Dictionary<int, HotkeyState> _hotkeyStatesCache = new Dictionary<int, HotkeyState>(); 

        public HotkeyStatesMonitor(IMetaPubSub messenger)
        {
            _messenger = messenger;

            _messenger.Subscribe<HotkeyStateChangedMessage>(OnHotkeyStateChanged);
            _messenger.Subscribe<GetHotkeyStatesMessage>(OnGetHotkeyStates);
        }

        Task OnHotkeyStateChanged(HotkeyStateChangedMessage msg)
        {
            _hotkeyStatesCache[msg.HotkeyId] = msg.State;

            return Task.CompletedTask;
        }

        async Task OnGetHotkeyStates(GetHotkeyStatesMessage msg)
        {
            var dictionaryCopy = new Dictionary<int, HotkeyState>(_hotkeyStatesCache);

            var reply = new GetHotkeyStatesMessageReply(dictionaryCopy);

            try
            {
                await _messenger.Publish(reply);
            }
            catch (Exception)
            {
                // Silent handling
            }
        }
    }
}
