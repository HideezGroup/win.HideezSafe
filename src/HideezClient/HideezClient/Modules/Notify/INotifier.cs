﻿using System;
using HideezClient.Controls;
using HideezClient.Models;
using System.Threading.Tasks;
using Hideez.ARM;

namespace HideezClient.Modules
{
    interface INotifier
    {
        void ShowInfo(string title, string message, NotificationOptions options = null);
        void ShowInfo(string notificationId, string title, string message, NotificationOptions options = null);

        void ShowWarn(string title, string message, NotificationOptions options = null);
        void ShowWarn(string notificationId, string title, string message, NotificationOptions options = null);

        void ShowError(string title, string message, NotificationOptions options = null);
        void ShowError(string notificationId, string title, string message, NotificationOptions options = null);

        void ClearNotifications();

        void ShowStorageLoadingNotification(CredentialsLoadNotificationViewModel viewModel);
        void ShowDeviceNotAuthorizedNotification(Device device);
        void ShowDeviceIsLockedByCodeNotification(Device device);
        void ShowDeviceIsLockedByPinNotification(Device device);
        Task<Account> SelectAccountAsync(Account[] accounts, IntPtr hwnd);
        Task<bool> ShowAccountNotFoundNotification(string title, string message);
    }
}