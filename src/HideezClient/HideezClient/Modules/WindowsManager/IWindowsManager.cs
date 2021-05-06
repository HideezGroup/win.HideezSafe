using System;
using System.Threading.Tasks;
using HideezClient.Models;
using HideezClient.Controls;
using System.Drawing;
using Hideez.ARM;

namespace HideezClient.Modules
{
    public interface IWindowsManager
    {
        Task ActivateMainWindow();
        void InitializeMainWindow();
        void HideMainWindow();
        Task ActivateMainWindowAsync();
        Task HideMainWindowAsync();
        Task InitializeMainWindowAsync();
        
        Task<Bitmap> GetCurrentScreenImageAsync();
        event EventHandler<bool> MainWindowVisibleChanged;
        bool IsMainWindowVisible { get; }
        void CloseWindow(string id);


        Task<bool> ShowAccountNotFoundAsync(string message, string title = null);
        Task<Account> SelectAccountAsync(Account[] accounts, IntPtr hwnd);
        void ShowCredentialsLoading(CredentialsLoadNotificationViewModel viewModel);
        Task<bool> ShowUpdateAvailableNotification(string message, string title = null);

        Task<bool> ShowDeleteCredentialsPromptAsync();
        Task<bool> ShowDisconnectDevicePromptAsync(string deviceName);
        Task<bool> ShowRemoveDevicePromptAsync(string deviceName);
        Task<bool> ShowResetToDefaultHotkeysAsync();
    }
}
