using HideezClient.Extension;
using HideezClient.Messages;
using HideezClient.Messages.Dialogs.MasterPassword;
using HideezClient.Models;
using HideezClient.Modules.DeviceManager;
using HideezMiddleware.Localize;
using HideezClient.Mvvm;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Attributes;
using MvvmExtensions.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HideezClient.ViewModels.Dialog
{
    public class MasterPasswordViewModel: ObservableObject, IDialogViewModel
    {
        readonly IMetaPubSub _metaMessenger;
        readonly IDeviceManager _deviceManager;
        readonly byte[] _emptyBytes = new byte[0];

        readonly object initLock = new object();

        SecureString _secureCurrentPassword;
        SecureString _secureNewPassword;
        SecureString _secureConfirmPassword;

        bool _askOldPassword = false;
        bool _confirmNewPassword = false;
        bool _inProgress = false;
        string _errorMessage = string.Empty;

        DeviceModel _device;

        public event EventHandler ViewModelUpdated;
        public event EventHandler PasswordsCleared;

        public MasterPasswordViewModel(IMetaPubSub metaMessenger, IDeviceManager deviceManager)
        {
            _metaMessenger = metaMessenger;
            _deviceManager = deviceManager;

            RegisterDependencies();
        }

        #region Properties

        public SecureString SecureCurrentPassword
        {
            get { return _secureCurrentPassword; }
            set { Set(ref _secureCurrentPassword, value); }
        }

        public SecureString SecureNewPassword
        {
            get { return _secureNewPassword; }
            set { Set(ref _secureNewPassword, value); }
        }

        public SecureString SecureConfirmPassword
        {
            get { return _secureConfirmPassword; }
            set { Set(ref _secureConfirmPassword, value); }
        }

        // Properties received from service
        public bool AskOldPassword
        {
            get { return _askOldPassword; }
            set { Set(ref _askOldPassword, value); }
        }

        public bool ConfirmNewPassword
        {
            get { return _confirmNewPassword; }
            set { Set(ref _confirmNewPassword, value); }
        }

        //...

        // Current Password operation
        [DependsOn(nameof(AskOldPassword), nameof(ConfirmNewPassword))]
        public bool IsNewPassword
        {
            get
            {
                return !AskOldPassword && ConfirmNewPassword;
            }
        }

        [DependsOn(nameof(AskOldPassword), nameof(ConfirmNewPassword))]
        public bool IsEnterPassword
        {
            get
            {
                return !AskOldPassword && !ConfirmNewPassword;
            }
        }

        [DependsOn(nameof(AskOldPassword), nameof(ConfirmNewPassword))]
        public bool IsChangePassword
        {
            get
            {
                return AskOldPassword && ConfirmNewPassword;
            }
        }
        //...

        // PasswordBox visibility fields
        [DependsOn(nameof(IsEnterPassword), nameof(IsChangePassword))]
        public bool AskCurrentPassword
        {
            get
            {
                return IsEnterPassword || IsChangePassword;
            }
        }

        [DependsOn(nameof(IsNewPassword), nameof(IsChangePassword))]
        public bool AskNewPassword
        {
            get
            {
                return IsNewPassword || IsChangePassword;
            }
        }

        [DependsOn(nameof(IsNewPassword), nameof(IsChangePassword))]
        public bool AskConfirmPassword
        {
            get
            {
                return IsNewPassword || IsChangePassword;
            }
        }
        //...

        public bool InProgress
        {
            get { return _inProgress; }
            set { Set(ref _inProgress, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { Set(ref _errorMessage, value); }
        }

        public int MaxLenghtPassword
        {
            get { return 32; }
        }

        public int MinLenghtPassword
        {
            get
            {
                return 8; //Todo:
            }
        }

        public DeviceModel Device
        {
            get { return _device; }
            set { Set(ref _device, value); }
        }

        [DependsOn(nameof(Device))]
        public string SerialNo
        {
            get { return _device?.SerialNo; }
        }

        #endregion Properties

        #region Commands
        public ICommand ConfirmCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x => Task.Run(OnConfirm),
                    CanExecuteFunc = () => AreAllRequiredFieldsSet() && !InProgress,
                };
            }
        }

        public ICommand CancelCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(OnCancel);
                    },
                };
            }
        }
        #endregion

        public void Initialize(string deviceId, bool askOldPassword, bool confirmNewPassword)
        {
            lock (initLock)
            {
                if (Device == null && !string.IsNullOrWhiteSpace(deviceId))
                {
                    var device = _deviceManager.Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        Device = device;
                        Device.PropertyChanged += (s, e) => RaisePropertyChanged(e.PropertyName);
                    }

                    UpdateViewModel(deviceId, askOldPassword, confirmNewPassword);

                    _metaMessenger.Subscribe<ShowMasterPasswordUiMessage>(ShowMasterPasswordAsync);
                }
            }
        }

        Task ShowMasterPasswordAsync(ShowMasterPasswordUiMessage msg)
        {
            UpdateViewModel(msg.DeviceId, msg.OldPassword, msg.ConfirmPassword);

            return Task.CompletedTask;
        }

        void UpdateViewModel(string deviceId, bool askOldPassword, bool confirmNewPassword)
        {
            if (Device?.Id != deviceId)
                return;

            AskOldPassword = askOldPassword;
            ConfirmNewPassword = confirmNewPassword;

            ResetProgress();
            ViewModelUpdated?.Invoke(this, EventArgs.Empty);
        }

        bool AreAllRequiredFieldsSet()
        {
            if (IsNewPassword)
            {
                if (IsValidLength(SecureNewPassword, MinLenghtPassword, MaxLenghtPassword) != 0 ||
                    IsValidLength(SecureConfirmPassword, MinLenghtPassword, MaxLenghtPassword) != 0)
                    return false;
            }
            else if (IsEnterPassword)
            {
                if (IsValidLength(SecureCurrentPassword, 1, MaxLenghtPassword) != 0)
                    return false;
            }
            else if (IsChangePassword)
            {
                if (IsValidLength(SecureCurrentPassword, 1, MaxLenghtPassword) != 0 ||
                    IsValidLength(SecureNewPassword, MinLenghtPassword, MaxLenghtPassword) != 0 ||
                    IsValidLength(SecureConfirmPassword, MinLenghtPassword, MaxLenghtPassword) != 0)
                    return false;
            }

            return true;
        }

        void OnConfirm()
        {
            InProgress = true;
            ErrorMessage = string.Empty;

            // Gather data
            SecureString oldPassword = null;
            SecureString password = null;
            SecureString confirmPassword = null;

            if (IsNewPassword)
            {
                password = SecureNewPassword;
                confirmPassword = SecureConfirmPassword;
            }
            else if (IsEnterPassword)
            {
                password = SecureCurrentPassword;
            }
            else if (IsChangePassword)
            {
                oldPassword = SecureCurrentPassword;
                password = SecureNewPassword;
                confirmPassword = SecureConfirmPassword;
            }

            if (IsNewPassword || IsChangePassword)
            {
                if (!IsConfirmPasswordCorrect(password, confirmPassword))
                {
                    _metaMessenger.Publish(new ShowErrorNotificationMessage(TranslationSource.Instance["MasterPassword.Error.PasswordsDontMatch"], notificationId: Device.Mac));
                    InProgress = false;
                    return;
                }
            }

            var passwordBytes = password != null ? password.ToUtf8Bytes() : _emptyBytes;
            var oldPasswordBytes = oldPassword != null ? oldPassword.ToUtf8Bytes() : _emptyBytes;

            _metaMessenger.Publish(new SendMasterPasswordMessage(Device.Id, passwordBytes, oldPasswordBytes));

            ClearPasswords();
        }

        void OnCancel()
        {
            _metaMessenger.Publish(new MasterPasswordCancelledMessage(Device.Id));
        }

        void ClearPasswords()
        {
            SecureCurrentPassword?.Clear();
            SecureNewPassword?.Clear();
            SecureConfirmPassword?.Clear();

            PasswordsCleared?.Invoke(this, EventArgs.Empty);
        }

        void ResetProgress()
        {
            ClearPasswords();
            InProgress = false;
        }

        /// <summary>
        /// Check if Password is of sufficient length
        /// </summary>
        /// <returns>
        /// Returns 0 if password is of sufficient length.
        /// Returns 1 if pin is to long.
        /// Returns -1 if pin is to short.
        /// </returns>
        int IsValidLength(SecureString pin, int minLength, int maxLengh)
        {
            if (pin == null)
                return -1;

            if (pin.Length < minLength)
                return -1;
            else if (pin.Length > maxLengh)
                return 1;

            return 0;
        }

        bool IsConfirmPasswordCorrect(SecureString password, SecureString confirmPassword)
        {
            if (password == null || confirmPassword == null)
                return false;

            return password.IsEqualTo(confirmPassword);
        }

        public void OnClose()
        {
            _metaMessenger.Unsubscribe<ShowMasterPasswordUiMessage>(ShowMasterPasswordAsync);
        }
    }
}
