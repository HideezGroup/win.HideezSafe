﻿using HideezSafe.ViewModels;
using HideezSafe.Modules.ActionHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HideezSafe.Modules
{
    public interface IWindowsManager
    {
        void ActivateMainWindow();
        Task ActivateMainWindowAsync();
        event EventHandler<bool> MainWindowVisibleChanged;
        bool IsMainWindowVisible { get; }
        void ShowDialogAddCredential(DeviceViewModel device);

        void ShowInfo(string message, string title = null);
        void ShowWarning(string message, string title = null);
        void ShowError(string message, string title = null);
        Task<Account> SelectAccountAsync(Account[] accounts);
    }
}
