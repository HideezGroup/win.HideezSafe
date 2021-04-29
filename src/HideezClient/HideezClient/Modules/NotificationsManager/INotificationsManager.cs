using HideezClient.Controls;
using HideezClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezClient.Modules.NotificationsManager
{
    interface INotificationsManager
    {
        void ShowNotification(string notificationId, string title, string message, NotificationIconType notificationType, NotificationOptions options = null);
        void ShowStorageLoadingNotification(CredentialsLoadNotificationViewModel viewModel);
        Task<bool> ShowAccountNotFoundNotification(string title, string message);
        Task<Account> SelectAccountAsync(Account[] accounts, IntPtr hwnd);
        Task<bool> ShowApplicationUpdateAvailableNotification(string title, string message);
        void ShowClientOpeningFromTaskbarNotification();
        void ClearNotifications();
    }
}
