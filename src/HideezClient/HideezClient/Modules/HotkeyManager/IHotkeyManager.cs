using HideezClient.Models;
using System.Threading.Tasks;

namespace HideezClient.Modules.HotkeyManager
{
    internal interface IHotkeyManager
	{
        Task<string> GetEnabledKeystrokeForAction(UserAction action);

        Task<bool> IsUniqueKeystroke(int hotkeyId, string hotkey);

    }
}