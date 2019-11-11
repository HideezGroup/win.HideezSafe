﻿using Hideez.SDK.Communication.PasswordManager;
using HideezClient.HideezServiceReference;
using HideezClient.Models;
using HideezClient.Modules;
using HideezClient.Modules.ServiceProxy;
using HideezClient.Mvvm;
using HideezClient.Utilities;
using HideezClient.Views;
using MvvmExtensions.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HideezClient.ViewModels
{
    class AddCredentialViewModel : ObservableObject
    {
        private readonly IWindowsManager windowsManager;

        private string selectedLogin;
        private bool isInProgress;

        public AddCredentialViewModel(IWindowsManager windowsManager)
        {
            this.windowsManager = windowsManager;
            Logins = new ObservableCollection<string>(GetAllUserNames());
        }

        public ICommand SaveCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        if (x is AddCredentialView view)
                        {
                            SaveCredential(view);
                        }
                    },
                };
            }
        }

        public Device Device { get; set; }

        public ObservableCollection<string> Logins { get; }

        public string SelectedLogin
        {
            get { return selectedLogin; }
            set { Set(ref selectedLogin, value); }
        }

        public bool IsInProgress
        {
            get { return isInProgress; }
            set { Set(ref isInProgress, value); }
        }

        private List<string> GetAllUserNames()
        {
            List<string> result = new List<string>();

            // Get all "real" local usernames
            SelectQuery query = new SelectQuery("Select * from Win32_UserAccount Where LocalAccount = True");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

            var localUsers = searcher.Get().Cast<ManagementObject>().Where(
                u => (bool)u["LocalAccount"] == true &&
                     (bool)u["Disabled"] == false &&
                     (bool)u["Lockout"] == false &&
                     int.Parse(u["SIDType"].ToString()) == 1 &&
                     u["Name"].ToString() != "HomeGroupUser$");

            // Try to get MS Account for each local username and if found use it instead of local username
            foreach (ManagementObject user in localUsers)
            {
                string msName = LocalToMSAccountConverter.TryTransformToMS(user["Name"] as string);

                if (!String.IsNullOrWhiteSpace(msName))
                    result.Add(@"MicrosoftAccount\" + msName);
                else
                    result.Add(new SecurityIdentifier(user["SID"].ToString()).Translate(typeof(NTAccount)).ToString());
            }

            return result;
        }

        private void SaveCredential(AddCredentialView view)
        {
            if (string.IsNullOrWhiteSpace(SelectedLogin))
            {
                windowsManager.ShowWarn($"Login cannot be empty. Please, enter PC login.");
                return;
            }

            if (view.passwordBox.SecurePassword.Length == 0)
            {
                windowsManager.ShowWarn($"Password cannot be empty. Please, enter PC password.");
                return;
            }

            IsInProgress = true;
            var login = SelectedLogin;
            var pass = view.passwordBox.Password;

            Task.Run(async () =>
            {
                try
                {
                    if (!Device.IsConnected)
                        throw new Exception("Remote device is not connected");

                    if (!Device.IsInitialized)
                        throw new Exception("Remote device is not initialized");

                    await Device.PasswordManager.SavePcUnlockCredentials(login, pass);
                    IsInProgress = false;
                    Application.Current.Dispatcher.Invoke(view.Close);
                }
                catch (FaultException<HideezServiceFault> ex)
                {
                    windowsManager.ShowError($"An error occured while saving credentials:{Environment.NewLine}{ex.FormattedMessage()}");
                }
                catch (Exception ex)
                {
                    windowsManager.ShowError($"An error occured while saving credentials:{Environment.NewLine}{ex.Message}");
                }
                finally
                {
                    IsInProgress = false;
                }
            });
        }
    }
}
