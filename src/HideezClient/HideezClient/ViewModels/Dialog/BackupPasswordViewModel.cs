using HideezClient.Extension;
using HideezClient.Messages;
using HideezClient.Messages.Dialogs.BackupPassword;
using HideezMiddleware.Localize;
using HideezClient.Mvvm;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Attributes;
using MvvmExtensions.Commands;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Input;
using HideezClient.Messages.Dialogs;
using HideezClient.Dialogs;

namespace HideezClient.ViewModels.Dialog
{
    public class BackupPasswordViewModel : ObservableObject, IDialogViewModel
    {
        readonly IMetaPubSub _metaMessenger;
        readonly byte[] _emptyBytes = new byte[0];

        readonly object initLock = new object();

        SecureString _secureCurrentPassword;
        SecureString _secureNewPassword;
        SecureString _secureConfirmPassword;

        bool _isNewPassword = false;
        bool _inProgress = false;
        bool _isSuccessful = false;
        bool _isError = false;
        bool _needInputPasswords = true;
        string _errorMessage = string.Empty;
        string _progressMessage = TranslationSource.Instance["Pin.InProgress"];
        string _fileName = string.Empty;
        string _deviceId = string.Empty;

        public event EventHandler ViewModelUpdated;
        public event EventHandler PasswordsCleared;

        public BackupPasswordViewModel(IMetaPubSub metaMessenger)
        {
            _metaMessenger = metaMessenger;

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

        public bool IsNewPassword
        {
            get { return _isNewPassword; }
            set { Set(ref _isNewPassword, value); }
        }

        public bool InProgress
        {
            get { return _inProgress; }
            set { Set(ref _inProgress, value); }
        }
        
        public bool NeedInputPassword
        {
            get { return _needInputPasswords; }
            set { Set(ref _needInputPasswords, value); }
        }

        public bool IsSuccessful
        {
            get { return _isSuccessful; }
            set { Set(ref _isSuccessful, value); }
        }

        public bool IsError
        {
            get { return _isError; }
            set { Set(ref _isError, value); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { Set(ref _errorMessage, value); }
        }

        public string ProgressMessage
        {
            get { return _progressMessage; }
            set { Set(ref _progressMessage, value); }
        }

        [DependsOn(nameof(IsError), nameof(IsSuccessful))]
        public bool IsFinished
        {
            get => IsError || IsSuccessful;
        }

        public int MaxLenghtPassword
        {
            get { return 32; }
        }

        public int MinLenghtPassword
        {
            get
            {
                return 8;
            }
        }

        public string FileName
        {
            get { return _fileName.Split('\\').LastOrDefault(); }
            set { Set(ref _fileName, value); }
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

        public ICommand CloseCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(OnCloseCommand);
                    },
                };
            }
        }

        public ICommand ShowInFolderCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(OnShowInFolder);
                    },
                };
            }
        }
        #endregion

        public void Initialize(string deviceId, string fileName, bool isNewPassword)
        {
            lock (initLock)
            {
                _deviceId = deviceId;

                UpdateViewModel(deviceId, fileName, isNewPassword);

                _metaMessenger.Subscribe<ShowBackupPasswordUiMessage>(ShowBackupPasswordAsync);
                _metaMessenger.Subscribe<SetResultUIBackupPasswordMessage>(SetResultUIBackupPasswordAsync);
                _metaMessenger.Subscribe<SetProgressUIBackupPasswordMessage>(SetProgressUIBackupPasswordAsync);
            }
        }

        Task SetProgressUIBackupPasswordAsync(SetProgressUIBackupPasswordMessage arg)
        {
            ProgressMessage = arg.Text;

            return Task.CompletedTask;
        }

        Task SetResultUIBackupPasswordAsync(SetResultUIBackupPasswordMessage arg)
        {
            InProgress = false;

            if (arg.IsSuccessful)
                IsSuccessful = true;
            else
            {
                if (!string.IsNullOrWhiteSpace(arg.ErrorMessage))
                    ErrorMessage = arg.ErrorMessage;
                else
                    ErrorMessage = TranslationSource.Instance["BackupPassword.Failed"];
                IsError = true;
            }

            return Task.CompletedTask;
        }

        Task ShowBackupPasswordAsync(ShowBackupPasswordUiMessage arg)
        {
            UpdateViewModel(arg.DeviceId, arg.BackupFileName, arg.IsNewPassword);

            return Task.CompletedTask;
        }

        void UpdateViewModel(string deviceId, string fileName, bool isNewPassword)
        {
            if (_deviceId != deviceId)
                return;

            FileName = fileName;
            IsNewPassword = isNewPassword;
            
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
            else
            {
                if (IsValidLength(SecureCurrentPassword, 1, MaxLenghtPassword) != 0)
                    return false;
            }

            return true;
        }

        void OnConfirm()
        {
            NeedInputPassword = false;
            InProgress = true;
            ErrorMessage = string.Empty;

            // Gather data
            SecureString password = null;
            SecureString confirmPassword = null;

            if (IsNewPassword)
            {
                password = SecureNewPassword;
                confirmPassword = SecureConfirmPassword;
            }
            else
            {
                password = SecureCurrentPassword;
            }

            if (IsNewPassword)
            {
                if (!IsConfirmPasswordCorrect(password, confirmPassword))
                {
                    _metaMessenger.Publish(new ShowErrorNotificationMessage(TranslationSource.Instance["BackupPassword.Error.BPsDontMatch"]));
                    NeedInputPassword = true;
                    InProgress = false;
                    return;
                }
            }

            var passwordBytes = password != null ? password.ToUtf8Bytes() : _emptyBytes;

            _metaMessenger.Publish(new SendBackupPasswordMessage(_deviceId, passwordBytes));
        }

        void OnCancel()
        {
            _metaMessenger.Publish(new BackupPasswordCancelledMessage(_deviceId));
        }

        void OnCloseCommand()
        {
            _metaMessenger.Publish(new HideDialogMessage(typeof(BackupPasswordDialog)));
        }

        void OnShowInFolder()
        {
            var index = _fileName.LastIndexOf('\\');
            string folderPath = _fileName.Substring(0, index);

            Process.Start(folderPath);

            _metaMessenger.Publish(new HideDialogMessage(typeof(BackupPasswordDialog)));
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
            NeedInputPassword = true;
            InProgress = false;
            IsSuccessful = false;
            IsError = false;
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
            _metaMessenger.Unsubscribe<ShowBackupPasswordUiMessage>(ShowBackupPasswordAsync);
            _metaMessenger.Unsubscribe<SetResultUIBackupPasswordMessage>(SetResultUIBackupPasswordAsync);
            _metaMessenger.Unsubscribe<SetProgressUIBackupPasswordMessage>(SetProgressUIBackupPasswordAsync);
        }
    }
}
