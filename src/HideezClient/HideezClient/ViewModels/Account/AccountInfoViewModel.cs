﻿using DynamicData;
using Hideez.SDK.Communication.PasswordManager;
using HideezClient.Utilities;
using ReactiveUI.Fody.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HideezClient.ViewModels
{
    public class AccountInfoViewModel
    {
        private readonly AccountRecord accountRecord;

        public AccountInfoViewModel(AccountRecord accountRecord)
        {
            this.accountRecord = accountRecord;

            if (accountRecord.Apps != null)
            {
                AppsUrls.AddRange(AccountUtility.Split(accountRecord.Apps));
            }
            if (accountRecord.Urls != null)
            {
                AppsUrls.AddRange(AccountUtility.Split(accountRecord.Urls));
            }
        }

        public bool CanEditable { get; set; } = true;
        public bool CanVisible { get; set; } = true;
        public bool CanDelete { get; set; } = true;

        public ushort Key { get { return accountRecord.Key; } }
        public string Name { get { return accountRecord.Name; } }
        public string Login { get { return accountRecord.Login; } }
        public bool HasOpt { get { return accountRecord.HasOtp; } }
        public bool IsPrimary { get { return accountRecord.IsPrimary; } }
        public ObservableCollection<string> AppsUrls { get; } = new ObservableCollection<string>();
        public AccountRecord AccountRecord { get { return accountRecord; } }
    }
}
