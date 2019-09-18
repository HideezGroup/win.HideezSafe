﻿using HideezClient.ViewModels;
using HideezClient.Modules.ActionHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HideezClient.Models;
using HideezClient.Controls;

namespace HideezClient.Modules
{
    public interface IWindowsManager
    {
        void ActivateMainWindow();
        Task ActivateMainWindowAsync();
        event EventHandler<bool> MainWindowVisibleChanged;
        bool IsMainWindowVisible { get; }
        void ShowDialogAddCredential(Device device);
        void CloseWindow(Guid id);

        void ShowInfo(string message, string title = null, string notificationId = null);
        void ShowWarn(string message, string title = null, string notificationId = null);
        void ShowError(string message, string title = null, string notificationId = null);
        Task<Account> SelectAccountAsync(Account[] accounts, IntPtr hwnd);
        void ShowCredentialsLoading(CredentialsLoadNotificationViewModel viewModel);
        Task ShowDeviceLockedAsync();
        void ShowDeviceNotAuthorized(Device device);
    }
}
