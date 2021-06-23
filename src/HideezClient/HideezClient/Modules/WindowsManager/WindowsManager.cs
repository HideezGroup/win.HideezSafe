using HideezClient.Mvvm;
using HideezClient.ViewModels;
using HideezClient.Views;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HideezClient.Models;
using HideezClient.Controls;
using HideezClient.Messages;
using System.Threading;
using System.Drawing;
using HideezClient.Utilities;
using System.Windows.Interop;
using HideezClient.Dialogs;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using HideezMiddleware.Settings;
using HideezClient.Models.Settings;
using Hideez.SDK.Communication.Log;
using HideezClient.Modules.Log;
using HideezClient.Modules.NotificationsManager;
using Meta.Lib.Modules.PubSub;
using HideezMiddleware.IPC.Messages;
using HideezClient.Messages.Dialogs;
using HideezClient.Messages.Dialogs.Pin;
using HideezClient.Messages.Dialogs.MasterPassword;
using HideezClient.Messages.Dialogs.BackupPassword;
using HideezClient.Messages.Dialogs.Wipe;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using HideezClient.Extension;

namespace HideezClient.Modules
{
    class WindowsManager : IWindowsManager
    {
        private readonly ViewModelLocator _viewModelLocator;
        private string titleNotification;
        private readonly Logger log = LogManager.GetCurrentClassLogger(nameof(WindowsManager));
        private bool isMainWindowVisible;
        private readonly ISettingsManager<ApplicationSettings> _settingsManager;
        readonly INotificationsManager _notificationsManager;
        readonly IMetaPubSub _messenger;

        readonly object hideDialogLock = new object();
        readonly object closedDialogLock = new object();

        List<BaseDialog> _dialogs = new List<BaseDialog>();

        bool _initialized = false;
        int _mainWindowActivationInterlock = 0;

        public event EventHandler<bool> MainWindowVisibleChanged;

        public WindowsManager(ViewModelLocator viewModelLocator, INotificationsManager notificationsManager,
             ISettingsManager<ApplicationSettings> settingsManager, IMetaPubSub messenger)
        {
            _viewModelLocator = viewModelLocator;
            _settingsManager = settingsManager;
            _notificationsManager = notificationsManager;
            _messenger = messenger;

            _messenger.Subscribe<ShowWarningNotificationMessage>(ShowWarn);
            _messenger.Subscribe<ShowInfoNotificationMessage>(ShowInfo);
            _messenger.Subscribe<ShowErrorNotificationMessage>(ShowError);
            _messenger.Subscribe<ShowLockNotificationMessage>(ShowLockNotification);
            _messenger.Subscribe<ShowLowBatteryNotificationMessage>(ShowLowBatteryNotification);
            _messenger.Subscribe<ShowMinimizedWindowHelpNotificationMessage>(ShowMinifiedWindowHelpNofitication);

            _messenger.TrySubscribeOnServer<WorkstationUnlockedMessage>(ClearNotifications);

            _messenger.TrySubscribeOnServer<UserNotificationMessage>((m)=>ShowInfo(new ShowInfoNotificationMessage(m.Message, notificationId:m.NotificationId)));
            _messenger.TrySubscribeOnServer<UserErrorMessage>((m) => ShowError(new ShowErrorNotificationMessage(m.Message, notificationId: m.NotificationId)));

            _messenger.Subscribe<ShowButtonConfirmUiMessage>(ShowButtonConfirmAsync, msg => !ContainsDialogType(typeof(PinDialog)));
            _messenger.Subscribe<ShowPinUiMessage>(ShowPinAsync, msg => !ContainsDialogType(typeof(PinDialog)));
            _messenger.Subscribe<ShowMasterPasswordUiMessage>(ShowMasterPasswordAsync, msg => !ContainsDialogType(typeof(MasterPasswordDialog)));
            _messenger.Subscribe<ShowBackupPasswordUiMessage>(ShowBackupPasswordAsync, msg => !ContainsDialogType(typeof(BackupPasswordDialog)));
            _messenger.Subscribe<ShowWipeDialogMessage>(ShowWipeDialogAsync, msg => !ContainsDialogType(typeof(WipeDialog)));

            _messenger.Subscribe<HideDialogMessage>(OnHideDialog);
            _messenger.Subscribe<HideAllDialogsMessage>(OnHideAllDialogs);

            _messenger.TrySubscribeOnServer<ShowActivationCodeUiMessage>(ShowActivationDialogAsync, msg => !ContainsDialogType(typeof(ActivationDialog)));
            _messenger.TrySubscribeOnServer<ShowClientMainWindowMessage>((p) => ActivateMainWindow());

            _messenger.Subscribe<ShowActivateMainWindowMessage>((p) => ActivateMainWindow());
        }

        #region MainWindow
        public Task ActivateMainWindow()
        {
            UIDispatcher.Invoke(() => OnActivateMainWindow(true));
            return Task.CompletedTask;
        }

        public void HideMainWindow()
        {
            UIDispatcher.Invoke(OnHideMainWindow);
        }

        public void InitializeMainWindow()
        {
            UIDispatcher.Invoke(OnInitializeMainWindow);
        }

        public async Task ActivateMainWindowAsync()
        {
            await UIDispatcher.InvokeAsync(() => OnActivateMainWindow(true));
        }

        public async Task HideMainWindowAsync()
        {
            await UIDispatcher.InvokeAsync(OnHideMainWindow);
        }

        public async Task InitializeMainWindowAsync()
        {
            await UIDispatcher.InvokeAsync(OnInitializeMainWindow);
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


        bool _isFirstActivation = true;
        private void OnActivateMainWindow(bool isNeedResize)
        {
            if (Interlocked.CompareExchange(ref _mainWindowActivationInterlock, 1, 0) == 0)
            {
                try
                {
                    if (MainWindow == null || !_initialized) return;

                    // event is only subscribed to once
                    UnsubscribeToMainWindowEvent();
                    SubscribeToMainWindowEvent();

                    MainWindow.Show();
                    if (isNeedResize)
                    {
                        if (_settingsManager.Settings.MaximizeWindowsOnOpening)
                        {
                            MainWindow.WindowState = WindowState.Maximized;
                            MainWindow.WindowStyle = WindowStyle.None;
                        }
                        else
                        {
                            if (MainWindow.WindowState != WindowState.Maximized)
                                MainWindow.WindowState = WindowState.Normal;

                            if (_isFirstActivation)
                            {
                                System.Windows.Forms.Screen screen = GetCurrentScreen();
                                if (screen != null)
                                {
                                    var dpiTransform = MainWindow.GetDpiTransform();
                                    var workingAreaForWindowHeight = screen.WorkingArea.Height / dpiTransform.Y - MainWindow.Top;
                                    if (workingAreaForWindowHeight < MainWindow.MinHeight)
                                    {
                                        MainWindow.Top = 0;
                                        workingAreaForWindowHeight = screen.WorkingArea.Height / dpiTransform.Y - MainWindow.Top;
                                    }

                                    if (workingAreaForWindowHeight > 0 && workingAreaForWindowHeight < 770)
                                        MainWindow.Height = workingAreaForWindowHeight;
                                    else MainWindow.Height = 770;
                                }
                            }
                        }
                    }

                    if (MainWindow.WindowState == WindowState.Minimized)
                    {
                        MainWindow.WindowState = WindowState.Normal;
                    }

                    MainWindow.Activate();
                    MainWindow.Topmost = true;
                    MainWindow.Topmost = false;
                    MainWindow.Focus();

                    _isFirstActivation = false;
                }
                finally
                {
                    Interlocked.Exchange(ref _mainWindowActivationInterlock, 0);
                }
            }
        }

        System.Windows.Forms.Screen GetCurrentScreen()
        {
            IntPtr foregroundWindow = Win32Helper.GetForegroundWindow();
            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(foregroundWindow);
            return screen;
        }

        private void OnHideMainWindow()
        {
            if (MainWindow == null || !_initialized) return;

            // event is only subscribed to once
            UnsubscribeToMainWindowEvent();
            SubscribeToMainWindowEvent();

            if (MainWindow.WindowState == WindowState.Normal)
            {
                MainWindow.WindowState = WindowState.Minimized;
            }

            MainWindow.Hide();
        }

        private void DisposeMainWindow()
        {
            if (MainWindow == null) return;

            UnsubscribeToMainWindowEvent();

            MainWindow.Hide();
            MainWindow.Close();
            Application.Current.MainWindow = null;
        }

        private void OnInitializeMainWindow()
        {
            DisposeMainWindow();

            if (_settingsManager.Settings.UseSimplifiedUI)
                Application.Current.MainWindow = new SimpleMainView();
            else
                Application.Current.MainWindow = new MainWindowView();

            _initialized = true;
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

        private void OnMainWindowVisibleChanged(bool isVisible)
        {
            try
            {
                MainWindowVisibleChanged?.Invoke(this, isVisible);
                log.WriteLine($"Main window is visible changed: {isVisible}");

                if (!isVisible)
                    _ = _messenger.Publish(new MainWindowClosedMessage());
            }
            catch (Exception ex)
            {
                log.WriteLine(ex);
            }
        }

        public void CloseWindow(string id)
        {
            UIDispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.DataContext is IRequireViewIdentification vm && vm.ObservableId == id)
                    {
                        window.Close();
                    }
                }
            });
        }
        
        public void RestartApplication()
        {
            log.WriteLine("Hideez Client restart");
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            UIDispatcher.Invoke(Application.Current.Shutdown);
        }

        private void SetStartupLocation(Window window, bool mainWindowWasOpen, bool hideMainWindow = false)
        {
            if (mainWindowWasOpen)
            {
                window.Owner = MainWindow;
                if (hideMainWindow)
                {
                    MainWindow?.Hide();
                }
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
        #endregion

        #region Notifications
        private Task ShowLockNotification(ShowLockNotificationMessage message)
        {
            UIDispatcher.Invoke(() => _notificationsManager.ShowNotification(message.NotificationId, message.Title ?? GetTitle(), message.Message, NotificationIconType.Lock, message.Options));
            return Task.CompletedTask;
        }

        private Task ShowError(ShowErrorNotificationMessage message)
        {
            UIDispatcher.Invoke(() => _notificationsManager.ShowNotification(message.NotificationId, message.Title ?? GetTitle(), message.Message, NotificationIconType.Error, message.Options));
            return Task.CompletedTask;
        }

        private Task ShowWarn(ShowWarningNotificationMessage message)
        {
            UIDispatcher.Invoke(() => _notificationsManager.ShowNotification(message.NotificationId, message.Title ?? GetTitle(), message.Message, NotificationIconType.Warn, message.Options));
            return Task.CompletedTask;
        }

        private Task ShowInfo(ShowInfoNotificationMessage message)
        {
            UIDispatcher.Invoke(() => _notificationsManager.ShowNotification(message.NotificationId, message.Title ?? GetTitle(), message.Message, NotificationIconType.Info, message.Options));
            return Task.CompletedTask;
        }

        private Task ShowLowBatteryNotification(ShowLowBatteryNotificationMessage message)
        {
            UIDispatcher.Invoke(() => _notificationsManager.ShowNotification(message.NotificationId, message.Title ?? GetTitle(), message.Message, NotificationIconType.Warn, message.Options));
            return Task.CompletedTask;
        }

        public Task<bool> ShowUpdateAvailableNotification(string message, string title = null)
        {
            return UIDispatcher.Invoke(() => _notificationsManager.ShowApplicationUpdateAvailableNotification(message, title));
        }

        private Task ClearNotifications(WorkstationUnlockedMessage message)
        {
            UIDispatcher.Invoke(() => _notificationsManager.ClearNotifications());
            return Task.CompletedTask;
        }

        public Task<bool> ShowAccountNotFoundAsync(string message, string title = null)
        {
            return UIDispatcher.Invoke(() => _notificationsManager.ShowAccountNotFoundNotification(title ?? GetTitle(), message));
        }

        private Task ShowMinifiedWindowHelpNofitication(ShowMinimizedWindowHelpNotificationMessage msg)
        {
            UIDispatcher.Invoke(() => _notificationsManager.ShowMinifiedWindowHelpNotification());
            return Task.CompletedTask;
        }

        private string GetTitle()
        {
            if (titleNotification == null)
            {
                // Commented, because assembly name does not contain a space between two words and 
                // assembly name will not be changed just for notification titles
                //titleNotification = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}";
                titleNotification = "Hideez Client";
            }

            return titleNotification;
        }

        public Task<Account> SelectAccountAsync(Account[] accounts, IntPtr hwnd)
        {
            return UIDispatcher.Invoke(() => _notificationsManager.SelectAccountAsync(accounts, hwnd));
        }

        public void ShowCredentialsLoading(CredentialsLoadNotificationViewModel viewModel)
        {
            UIDispatcher.Invoke(() => _notificationsManager.ShowStorageLoadingNotification(viewModel));
        }
        #endregion

        #region Dialogs
        void ShowDialog(BaseDialog baseDialog)
        {
            if (MainWindow is MetroWindow metroWindow)
            {
                baseDialog.Closed += DialogClosed;
                OnActivateMainWindow(false);
                metroWindow.ShowMetroDialogAsync(baseDialog);
                _dialogs.Add(baseDialog);
            }
        }

        Task ShowButtonConfirmAsync(ShowButtonConfirmUiMessage message)
        {
            UIDispatcher.Invoke(() =>
            {
                var vm = _viewModelLocator.PinViewModel;
                vm.Initialize(message.DeviceId, true, false, false);
                var pinView = new PinDialog(vm);
                ShowDialog(pinView);
            });

            return Task.CompletedTask;
        }

        Task ShowPinAsync(ShowPinUiMessage message)
        {
            UIDispatcher.Invoke(() =>
            {
                var vm = _viewModelLocator.PinViewModel;
                vm.Initialize(message.DeviceId, false, message.OldPin, message.ConfirmPin);
                var pinView = new PinDialog(vm);
                ShowDialog(pinView);
            });

            return Task.CompletedTask;
        }

        Task ShowMasterPasswordAsync(ShowMasterPasswordUiMessage message)
        {
            UIDispatcher.Invoke(() =>
            {
                var vm = _viewModelLocator.MasterPasswordViewModel;
                vm.Initialize(message.DeviceId, message.OldPassword, message.ConfirmPassword);
                var masterPasswordView = new MasterPasswordDialog(vm);
                ShowDialog(masterPasswordView);
            });

            return Task.CompletedTask;
        }

        Task ShowBackupPasswordAsync(ShowBackupPasswordUiMessage message)
        {
            UIDispatcher.Invoke(() =>
            {
                var vm = _viewModelLocator.BackupPasswordViewModel;
                vm.Initialize(message.DeviceId, message.BackupFileName, message.IsNewPassword);
                var backupPasswordView = new BackupPasswordDialog(vm);
                ShowDialog(backupPasswordView);
            });

            return Task.CompletedTask;
        }

        Task ShowWipeDialogAsync(ShowWipeDialogMessage msg)
        {
            UIDispatcher.Invoke(() =>
            {
                var vm = _viewModelLocator.WipeViewModel;
                vm.Initialize(msg.DeviceId);
                var wipeView = new WipeDialog(vm);
                ShowDialog(wipeView);
            });

            return Task.CompletedTask;
        }

        Task ShowActivationDialogAsync(ShowActivationCodeUiMessage obj)
        {
            try
            {
                UIDispatcher.Invoke(() =>
                {
                    var vm = _viewModelLocator.ActivationViewModel;
                    vm.Initialize(obj.DeviceId);
                    var activationView = new ActivationDialog(vm);
                    ShowDialog(activationView);
                });
            }
            catch { }

            return Task.CompletedTask;
        }

        void DialogClosed(object sender, EventArgs e)
        {
            lock (closedDialogLock)
            {
                if (sender is BaseDialog dialog)
                {
                    dialog.Closed -= DialogClosed;
                    _dialogs.Remove(dialog);
                }
            }
        }

        async Task OnHideDialog(HideDialogMessage message)
        {
            var dialog = _dialogs.FirstOrDefault(d => d.GetType() == message.DialogType);
            if (dialog != null)
            {
                try
                {
                    await dialog.Close();
                }
                catch { }
            }
        }

        void HideDialog(Type dialogType)
        {
            lock (hideDialogLock)
            {
                
            }
        }

        private async Task OnHideAllDialogs(HideAllDialogsMessage arg)
        {
            foreach (var dialog in _dialogs)
            {
                try
                {
                    await dialog?.Close();
                }
                catch { }
            }
        }


        bool ContainsDialogType(Type type)
        {
            lock (hideDialogLock)
            {
                return _dialogs.FirstOrDefault(d => d.GetType() == type) != null;
            }
        }
        #endregion

        #region ScreenImage
        public async Task<Bitmap> GetCurrentScreenImageAsync()
        {
            Bitmap screenShot = new Bitmap(1, 1);
            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.PrimaryScreen;
            var isMainWindowVisible = false;

            try
            {
                var hWndForegroundWindow = Win32Helper.GetForegroundWindow();
                screen = System.Windows.Forms.Screen.FromHandle(hWndForegroundWindow);

                if (MainWindow != null)
                {
                    IntPtr hWndMainWindow = new WindowInteropHelper(MainWindow).EnsureHandle();
                    isMainWindowVisible = hWndForegroundWindow == hWndMainWindow;
                }

                if (isMainWindowVisible)
                {
                    await HideMainWindowAsync();
                }

                screenShot = GetCurrentScreenImage(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);

                if (isMainWindowVisible)
                {
                    await ActivateMainWindowAsync();
                }

            }
            catch (Exception ex)
            {
                log.WriteLine(ex);
            }

            return screenShot;
        }

        public Bitmap GetCurrentScreenImage(double sourceX, double sourceY, double width, double height)
        {

            Bitmap bitmap = new Bitmap((int)width, (int)height);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen((int)sourceX, (int)sourceY, 0, 0, bitmap.Size);
            }

            return bitmap;
        }
        #endregion

        #region Messages
        public Task<bool> ShowDeleteCredentialsPromptAsync()
        {
            var vm = new MessageViewModel();
            vm.SetCaptionFormat("MessageBox.DeleteCredentials.Caption");
            vm.SetMessageFormat("MessageBox.DeleteCredentials.Message");
            return ShowMessageViewAsync(vm, "WarnIco", "Button.YesDelete", "Button.Cancel");
        }

        public Task<bool> ShowDisconnectDevicePromptAsync(string deviceName)
        {
            var vm = new MessageViewModel();
            vm.SetCaptionFormat("MessageBox.DisconectDevice.Caption", deviceName);
            vm.SetMessageFormat("MessageBox.DisconectDevice.Message", deviceName);
            return ShowMessageViewAsync(vm, "WarnIco", "Button.Yes", "Button.No");
        }

        public Task<bool> ShowRemoveDevicePromptAsync(string deviceName)
        {
            var vm = new MessageViewModel();
            vm.SetCaptionFormat("MessageBox.DeleteDevice.Caption", deviceName);
            vm.SetMessageFormat("MessageBox.DeleteDevice.Message", deviceName);
            return ShowMessageViewAsync(vm, "WarnIco", "Button.Yes", "Button.No");
        }

        public Task<bool> ShowResetToDefaultHotkeysAsync()
        {
            var vm = new MessageViewModel();
            vm.SetCaptionFormat("MessageBox.ResetHotkeys.Caption");
            vm.SetMessageFormat("MessageBox.ResetHotkeys.Message");
            return ShowMessageViewAsync(vm, "WarnIco", "Button.Yes", "Button.No");
        }

        public Task<bool> ShowDisabledUnlockPromptAsync()
        {
            var vm = new MessageViewModel();
            vm.SetCaptionFormat("MessageBox.DisablingUnlock.Caption");
            vm.SetMessageFormat("MessageBox.DisablingUnlock.Message");
            return ShowMessageViewAsync(vm, "WarnIco", "Button.Yes", "Button.No");
        }

        private Task<bool> ShowMessageViewAsync(MessageViewModel viewModel, string icoKey, string confirmButtonTextKey = "Button.Ok", string cancelButtonTextKey = "")
        {
            viewModel.Tcs = new TaskCompletionSource<bool>();

            UIDispatcher.InvokeAsync(() =>
            {
                var messageBox = new Dialogs.MessageDialog(icoKey, confirmButtonTextKey, cancelButtonTextKey)
                {
                    DataContext = viewModel
                };
                if (MainWindow is MetroWindow metroWindow)
                {
                    metroWindow.ShowMetroDialogAsync(messageBox);
                }
            });

            return viewModel.Tcs.Task;
        }
        #endregion
    }
}

