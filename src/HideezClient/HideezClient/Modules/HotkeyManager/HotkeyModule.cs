namespace HideezClient.Modules.HotkeyManager
{
    internal sealed class HotkeyModule
    {
        readonly IHotkeyManager _hotkeyManager;
        readonly IHotkeySettingsController _hotkeySettingsController;
        readonly IHotkeyStatesMonitor _hotkeyStatesMonitor;

        public HotkeyModule(IHotkeyManager hotkeyManager, 
            IHotkeySettingsController hotkeySettingsController, 
            IHotkeyStatesMonitor hotkeyStatesMonitor)
        {
            _hotkeyManager = hotkeyManager;
            _hotkeySettingsController = hotkeySettingsController;
            _hotkeyStatesMonitor = hotkeyStatesMonitor;
        }
    }
}
