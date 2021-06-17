using HideezClient.Mvvm;
using HideezClient.ViewModels;
using Unity;

namespace HideezClient.PageViewModels
{
    class SettingsPageViewModel : LocalizedObject
    {
        [Dependency]
        public ServiceViewModel Service { get; set; }

        [Dependency]
        public SoftwareUnlockSettingViewModel SoftwareUnlock { get; set; }

        [Dependency]
        public ReconnectPairedVaultsControlViewModel PairedVaultsReconnect { get; set; }

        [Dependency]
        public SecureFieldEntrySettingViewModel SecureFieldEntry { get; set; }

        [Dependency]
        public LanguageSettingViewModel Language { get; set; }

        [Dependency]
        public MaximizeWindowSettingViewModel MaximizeWindow { get; set; }
    }
}
