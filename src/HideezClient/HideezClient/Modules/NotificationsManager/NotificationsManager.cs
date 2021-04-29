using HideezClient.Controls;
using HideezClient.Controls.Notification.View;
using HideezClient.Models;
using HideezClient.Utilities;
using HideezClient.ViewModels;
using HideezClient.Views;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HideezClient.Modules.NotificationsManager
{
    class NotificationsManager: INotificationsManager
    {
        const string DEFAULT_NOTIFICATION_ID = "DEFAULT_NOTIFICATION_ID";

        readonly object lockObj = new object();

        Dictionary<string, NotificationsContainerWindow> windowsForNotifications = new Dictionary<string, NotificationsContainerWindow>();

        public NotificationsManager()
        {
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        }

        void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            ClearContainers();
        }

        public void ShowNotification(string notificationId, string title, string message, NotificationIconType notificationType, NotificationOptions options)
        {
            Screen screen = GetCurrentScreen();

            if (string.IsNullOrWhiteSpace(notificationId))
                notificationId = DEFAULT_NOTIFICATION_ID;


            SimpleNotification notification = new SimpleNotification(options ?? new NotificationOptions(), notificationType)
            {
                DataContext = new SimpleNotificationViewModel { Title = title, Message = message, ObservableId = notificationId, }
            };

            if (notification.Options.IsReplace)
            {
                var matchingNotificationViews = GetNotifications().Where(n => (n.DataContext as SimpleNotificationViewModel)?.ObservableId == notificationId);
                foreach (var notificationView in matchingNotificationViews)
                {
                    if (notificationView.DataContext is SimpleNotificationViewModel matchingNotificationViewModel)
                    {
                        //Close old notification when:
                        //1) Notification with matching ID is found but this ID is not the default
                        //2) Notification with default ID and matching content is found
                        if (matchingNotificationViewModel.ObservableId == notificationId)
                        {
                            if (matchingNotificationViewModel.ObservableId != DEFAULT_NOTIFICATION_ID)
                                notificationView.Close();
                            else
                            if (matchingNotificationViewModel.Message == message && matchingNotificationViewModel.Title == title)
                                notificationView.Close();
                        }
                    }
                }
            }

            // Do not create notifications without content
            if (string.IsNullOrWhiteSpace(message))
                return;

            AddNotification(screen, notification);
        }

        public void ClearNotifications()
        {
            var notifications = GetNotifications();
            foreach (NotificationBase notification in notifications)
            {
                notification.Close();
            }
        }

        public void ShowStorageLoadingNotification(CredentialsLoadNotificationViewModel viewModel)
        {
            Screen screen = GetCurrentScreen();

            var options = new NotificationOptions { CloseTimeout = TimeSpan.Zero, };

            CredentialsLoadNotification notification = new CredentialsLoadNotification(options)
            {
                DataContext = viewModel
            };

            if (notification.Options.IsReplace)
            {
                var matchingNotificationViews = GetNotifications().Where(n => (n.DataContext as CredentialsLoadNotificationViewModel)?.Device.NotificationsId == viewModel.Device.NotificationsId);
                // Displaying only one notification during connection is visually better. That's why we also close simple notifications
                // when displaying "Credentials Loading"
                var simpleNotificationViews = GetNotifications().Where(n => (n.DataContext as SimpleNotificationViewModel)?.ObservableId == viewModel.Device.NotificationsId);
                matchingNotificationViews = matchingNotificationViews.Concat(simpleNotificationViews); 
                foreach (var notificationView in matchingNotificationViews)
                {
                    notificationView.Close();
                }
            }
            AddNotification(screen, notification);
        }

        public async Task<bool> ShowAccountNotFoundNotification(string title, string message)
        {
            ClearContainers();
            Screen screen = GetCurrentScreen();

            TaskCompletionSource<bool> taskCompletionSourceForDialog = new TaskCompletionSource<bool>();

            var options = new NotificationOptions()
            {
                CloseTimeout = NotificationOptions.LongTimeout,
                SetFocus = true,
                CloseWhenDeactivate = true,
                TaskCompletionSource = taskCompletionSourceForDialog,
            };

            var viewModel = new SimpleNotificationViewModel()
            {
                Title = title,
                Message = message
            };
            AccountNotFoundNotification notification = new AccountNotFoundNotification(options)
            {
                DataContext = viewModel,
            };

            if (notification.Options.IsReplace)
            {
                var matchingNotificationViews = GetNotifications().Where(n => n is AccountNotFoundNotification).ToList();
                if (matchingNotificationViews.Count > 0)
                    foreach (var notificationView in matchingNotificationViews)
                    {
                        notificationView.ResetCloseTimer();
                    }
                else 
                    AddNotification(screen, notification, true);
            }

            var result = await taskCompletionSourceForDialog.Task;
            return result;
        }

        public async Task<Account> SelectAccountAsync(Account[] accounts, IntPtr hwnd)
        {
            ClearContainers();
            
            Screen screen = Screen.FromHandle(hwnd);

            TaskCompletionSource<bool> taskCompletionSourceForDialog = new TaskCompletionSource<bool>();

            var options = new NotificationOptions
            {
                SetFocus = true,
                CloseWhenDeactivate = true,
                Position = NotificationPosition.Bottom,
                TaskCompletionSource = taskCompletionSourceForDialog,
            };

            var viewModel = new AccountSelectorViewModel(accounts);
            AccountSelector notification = new AccountSelector(options)
            {
                DataContext = viewModel,
            };

            if (notification.Options.IsReplace)
            {
                var matchingNotificationViews = GetNotifications().Where(n => n is AccountSelector).ToList();
                if (matchingNotificationViews.Count > 0)
                    foreach (var notificationView in matchingNotificationViews)
                    {
                        notificationView.ResetCloseTimer();
                    }
                else AddNotification(screen, notification, true);
            }

            bool dialogResult = await taskCompletionSourceForDialog.Task;
            if (dialogResult)
            {
                return viewModel.SelectedAccount.Account;
            }

            return null;
        }

        public async Task<bool> ShowApplicationUpdateAvailableNotification(string title, string message)
        {
            Screen screen = GetCurrentScreen();

            TaskCompletionSource<bool> taskCompletionSourceForDialog = new TaskCompletionSource<bool>();
            
            var options = new NotificationOptions 
            { 
                CloseTimeout = TimeSpan.Zero,
                Position = NotificationPosition.Bottom,
                TaskCompletionSource = taskCompletionSourceForDialog,
            };

            var viewModel = new SimpleNotificationViewModel()
            {
                Title = title,
                Message = message,
            };

            UpdateAvailableNotification notification = new UpdateAvailableNotification(options)
            {
                DataContext = viewModel
            };

            if (notification.Options.IsReplace)
            {
                var matchingNotificationViews = GetNotifications().Where(n => n is UpdateAvailableNotification).ToList();
                if (matchingNotificationViews.Count > 0)
                    foreach (var notificationView in matchingNotificationViews)
                    {
                        notificationView.ResetCloseTimer();
                    }
                else
                    AddNotification(screen, notification, true);
            }

            var result = await taskCompletionSourceForDialog.Task;
            return result;
        }

        public void ShowClientOpeningFromTaskbarNotification()
        {
            Screen screen = GetCurrentScreen();

            var options = new NotificationOptions { CloseTimeout = TimeSpan.FromSeconds(10) };

            OpenClientFromTaskbarNotification notification = new OpenClientFromTaskbarNotification(options);

            if (notification.Options.IsReplace)
            {
                var matchingNotificationViews = GetNotifications().Where(n => n is OpenClientFromTaskbarNotification).ToList();

                foreach (var notificationView in matchingNotificationViews)
                {
                    notificationView.Close();
                }
            }
            AddNotification(screen, notification);
        }

        /// <summary>
        /// Find container for notification by screen if not found container create new and then add notification to container associated with the screen
        /// </summary>
        /// <param name="screen">Screen where show notification</param>
        /// <param name="notification">Notification</param>
        /// <param name="addForce">If tru Add to stack notifications if count notification more then max</param>
        void AddNotification(Screen screen, NotificationBase notification, bool addForce = false)
        {
            NotificationsContainerWindow window = null;
            lock (lockObj)
            {
                windowsForNotifications.TryGetValue(screen.DeviceName, out window);
                if (window == null)
                {
                    window = new NotificationsContainerWindow(screen);
                    window.Show();

                    windowsForNotifications[screen.DeviceName] = window;
                }
            }

            (window.DataContext as NotificationsContainerViewModel)?.AddNotification(notification, addForce);
            window.Show();
        }

        Screen GetCurrentScreen()
        {
            IntPtr foregroundWindow = Win32Helper.GetForegroundWindow();
            Screen screen = Screen.FromHandle(foregroundWindow);
            return screen;
        }

        IEnumerable<NotificationBase> GetNotifications()
        {
            return windowsForNotifications
                .Values
                .OfType<NotificationsContainerWindow>()
                .SelectMany(vm => (vm.DataContext as NotificationsContainerViewModel).Items)
                .ToList();
        }

        /// <summary>
        /// Close all windows for notification if screens is not valid
        /// Example: disconnect one or more monitors
        /// </summary>
        void ClearContainers()
        {
            try
            {
                lock (lockObj)
                {
                    foreach (var screen in windowsForNotifications.Keys.Except(Screen.AllScreens.Select(s => s.DeviceName)).ToArray())
                    {
                        if (windowsForNotifications.TryGetValue(screen, out NotificationsContainerWindow window))
                        {
                            window.Close();
                            windowsForNotifications.Remove(screen);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
