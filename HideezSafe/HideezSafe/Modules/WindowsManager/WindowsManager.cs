﻿using HideezSafe.Mvvm;
using HideezSafe.ViewModels;
using HideezSafe.Views;
using NLog;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace HideezSafe.Modules
{
    class WindowsManager : IWindowsManager
    {
        private readonly Logger log = LogManager.GetCurrentClassLogger();
        private bool isMainWindowVisible;

        public event EventHandler<bool> MainWindowVisibleChanged;

        public void ActivateMainWindow()
        {
            if (UIDispatcher.CheckAccess())
            {
                OnActivateMainWindow();
            }
            else
            {
                // Do non UI Thread stuff
                UIDispatcher.Invoke(OnActivateMainWindow);
            }
        }

        public async Task ActivateMainWindowAsync()
        {
            if (UIDispatcher.CheckAccess())
            {
                await Task.Run(new Action(OnActivateMainWindow));
            }
            else
            {
                // Do non UI Thread stuff
                await UIDispatcher.InvokeAsync(OnActivateMainWindow);
            }
        }

        public bool IsMainWindowVisible
        {
            get { return isMainWindowVisible; }
            private set
            {
                if (isMainWindowVisible != value)
                {
                    isMainWindowVisible = value;
                    OnMainWindowVisibleChanged(isMainWindowVisible);
                }
            }
        }

        private Window MainWindow { get { return Application.Current.MainWindow; } }

        private Dispatcher UIDispatcher { get { return Application.Current.Dispatcher; } }

        private void OnActivateMainWindow()
        {
            if (MainWindow == null) return;

            // event is only subscribed to once
            UnsubscribeToMainWindowEvent();
            SubscribeToMainWindowEvent();

            if (MainWindow.WindowState == WindowState.Minimized)
            {
                MainWindow.WindowState = WindowState.Normal;
            }

            MainWindow.Show();
            MainWindow.Activate();
        }

        private void SubscribeToMainWindowEvent()
        {
            MainWindow.IsVisibleChanged += MainWindow_IsVisibleChanged;
        }

        private void UnsubscribeToMainWindowEvent()
        {
            MainWindow.IsVisibleChanged -= MainWindow_IsVisibleChanged;
        }

        private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            IsMainWindowVisible = MainWindow.IsVisible;
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            IsMainWindowVisible = false;
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            IsMainWindowVisible = true;
        }

        private void OnMainWindowVisibleChanged(bool isVisivle)
        {
            try
            {
                MainWindowVisibleChanged?.Invoke(this, isVisivle);
                log.Info($"Main window is visible changed: {isVisivle}");
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
        }

        public void ShowDialogAddCredential(DeviceViewModel device)
        {
            var addCredentialWindow = new AddCredentialView();
            addCredentialWindow.Owner = MainWindow;
            if (addCredentialWindow.DataContext is AddCredentialViewModel viewModel)
            {
                viewModel.Device = device;
            }
            addCredentialWindow.ShowDialog();
        }

        public void ShowError(string message)
        {
            var title = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}";
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void ShowWarning(string message)
        {
            var title = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}";
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
