﻿using HideezClient.Extension;
using HideezClient.Messages;
using HideezClient.Models;
using HideezClient.Modules.DeviceManager;
using HideezClient.Mvvm;
using MvvmExtensions.Attributes;
using MvvmExtensions.Commands;
using System.Security;
using System.Windows.Input;
using System.Linq;
using System;
using HideezMiddleware.Localize;
using Meta.Lib.Modules.PubSub;
using HideezClient.Messages.Dialogs.Pin;
using System.Threading.Tasks;
using HideezClient.ViewModels.Dialog;

namespace HideezClient.ViewModels
{
    public class PinViewModel : ObservableObject, IDialogViewModel
    {
        readonly IMetaPubSub _metaMessenger;
        readonly IDeviceManager _deviceManager;
        readonly byte[] _emptyBytes = new byte[0];

        readonly object initLock = new object();

        SecureString _secureCurrentPin;
        SecureString _secureNewPin;
        SecureString _secureConfirmPin;

        bool _askButton = true;
        bool _askOldPin = false;
        bool _confirmNewPin = false;
        bool _inProgress = false;
        string _errorMessage = string.Empty;

        DeviceModel _device;

        public event EventHandler ViewModelUpdated;
        public event EventHandler PasswordsCleared;

        public PinViewModel(IMetaPubSub metaMessenger, IDeviceManager deviceManager)
        {
            _metaMessenger = metaMessenger;
            _deviceManager = deviceManager;

            RegisterDependencies();
        }

        #region Properties

        public SecureString SecureCurrentPin
        {
            get { return _secureCurrentPin; }
            set { Set(ref _secureCurrentPin, value); }
        }

        public SecureString SecureNewPin
        {
            get { return _secureNewPin; }
            set { Set(ref _secureNewPin, value); }
        }

        public SecureString SecureConfirmPin
        {
            get { return _secureConfirmPin; }
            set { Set(ref _secureConfirmPin, value); }
        }

        // Properties received from service
        public bool AskButton
        {
            get { return _askButton; }
            set { Set(ref _askButton, value); }
        }

        public bool AskOldPin
        {
            get { return _askOldPin; }
            set { Set(ref _askOldPin, value); }
        }

        public bool ConfirmNewPin
        {
            get { return _confirmNewPin; }
            set { Set(ref _confirmNewPin, value); }
        }

        //...

        // Current PIN operation
        [DependsOn(nameof(AskOldPin), nameof(ConfirmNewPin), nameof(AskButton))]
        public bool IsNewPin
        {
            get
            {
                return !AskOldPin && ConfirmNewPin && !AskButton;
            }
        }

        [DependsOn(nameof(AskOldPin), nameof(ConfirmNewPin), nameof(AskButton))]
        public bool IsEnterPin
        {
            get
            {
                return !AskOldPin && !ConfirmNewPin && !AskButton;
            }
        }

        [DependsOn(nameof(AskOldPin), nameof(ConfirmNewPin), nameof(AskButton))]
        public bool IsChangePin
        {
            get
            {
                return AskOldPin && ConfirmNewPin && !AskButton;
            }
        }
        //...

        // PasswordBox visibility fields
        [DependsOn(nameof(IsEnterPin), nameof(IsChangePin))]
        public bool AskCurrentPin
        {
            get
            {
                return IsEnterPin || IsChangePin;
            }
        }

        [DependsOn(nameof(IsNewPin), nameof(IsChangePin))]
        public bool AskNewPin
        {
            get
            {
                return IsNewPin || IsChangePin;
            }
        }

        [DependsOn(nameof(IsNewPin), nameof(IsChangePin))]
        public bool AskConfirmPin
        {
            get
            {
                return IsNewPin || IsChangePin;
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

        public int MaxLenghtPin
        {
            get { return 8; }
        }
        
        public int MinLenghtPin
        {
            get 
            {
                return Device.MinPinLength;
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

        [DependsOn(nameof(Device))]
        public int PinAttemptsLeft
        {
            get { return Device != null ? Device.PinAttemptsRemain : 0; }
        }

        #endregion Properties

        #region Commands
        public ICommand ConfirmCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x => OnConfirm(),
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
                        OnCancel();
                    },
                };
            }
        }
        #endregion

        public void Initialize(string deviceId, bool askButton, bool askOldPin, bool confirmNewPin)
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

                    UpdateViewModel(deviceId, askButton, askOldPin, confirmNewPin);

                    
                    _metaMessenger.Subscribe<ShowButtonConfirmUiMessage>(ShowButtonConfirmAsync);
                    _metaMessenger.Subscribe<ShowPinUiMessage>(ShowPinAsync);
                }
            }
        }

        Task ShowPinAsync(ShowPinUiMessage arg)
        {
            UpdateViewModel(arg.DeviceId, false, arg.OldPin, arg.ConfirmPin);

            return Task.CompletedTask;
        }

        Task ShowButtonConfirmAsync(ShowButtonConfirmUiMessage arg)
        {
            UpdateViewModel(arg.DeviceId, true, false, false);

            return Task.CompletedTask;
        }

        void UpdateViewModel(string deviceId, bool askButton, bool askOldPin, bool confirmNewPin)
        {
            if (Device?.Id != deviceId)
                return;

            AskButton = askButton;
            AskOldPin = askOldPin;
            ConfirmNewPin = confirmNewPin;

            ResetProgress();
            ViewModelUpdated?.Invoke(this, EventArgs.Empty);
        }

        bool AreAllRequiredFieldsSet()
        {
            if (IsNewPin)
            {
                if (IsValidLength(SecureNewPin, MinLenghtPin, MaxLenghtPin) != 0 || 
                    IsValidLength(SecureConfirmPin, MinLenghtPin, MaxLenghtPin) != 0)
                    return false;
            }
            else if (IsEnterPin)
            {
                if (IsValidLength(SecureCurrentPin, 1, MaxLenghtPin) != 0)
                    return false;
            }
            else if (IsChangePin)
            {
                if (IsValidLength(SecureCurrentPin, 1, MaxLenghtPin) != 0 ||
                    IsValidLength(SecureNewPin, MinLenghtPin, MaxLenghtPin) != 0 ||
                    IsValidLength(SecureConfirmPin, MinLenghtPin, MaxLenghtPin) != 0)
                    return false;
            }

            return true;
        }

        void OnConfirm()
        {
            InProgress = true;
            ErrorMessage = string.Empty;

            // Gather data
            SecureString oldPin = null;
            SecureString pin = null;
            SecureString confirmPin = null;

            if (IsNewPin)
            {
                pin = SecureNewPin;
                confirmPin = SecureConfirmPin;
            }
            else if (IsEnterPin)
            {
                pin = SecureCurrentPin;
            }
            else if (IsChangePin)
            {
                oldPin = SecureCurrentPin;
                pin = SecureNewPin;
                confirmPin = SecureConfirmPin;
            }

            if (IsNewPin || IsChangePin)
            {
                if (!IsConfirmPinCorrect(pin, confirmPin))
                {
                    _metaMessenger.Publish(new ShowErrorNotificationMessage(TranslationSource.Instance["Pin.Error.PinsDontMatch"], notificationId: Device.Mac));
                    InProgress = false;
                    return;
                }
            }

            var pinBytes = pin != null ? pin.ToUtf8Bytes() : _emptyBytes;
            var oldPinBytes = oldPin != null ? oldPin.ToUtf8Bytes() : _emptyBytes;

            _metaMessenger.Publish(new SendPinMessage(Device.Id, pinBytes, oldPinBytes));

            ClearPasswords();
        }

        void OnCancel()
        {
            _metaMessenger.Publish(new PinCancelledMessage(Device.Id));
        }

        void ClearPasswords()
        {
            SecureCurrentPin?.Clear();
            SecureNewPin?.Clear();
            SecureConfirmPin?.Clear();

            PasswordsCleared?.Invoke(this, EventArgs.Empty);
        }

        void ResetProgress()
        {
            ClearPasswords();
            InProgress = false;
        }

        /// <summary>
        /// Check if PIN is of sufficient length
        /// </summary>
        /// <returns>
        /// Returns 0 if pin is of sufficient length.
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

        bool IsConfirmPinCorrect(SecureString pin, SecureString confirmPin)
        {
            if (pin == null || confirmPin == null)
                return false;

            return pin.IsEqualTo(confirmPin);
        }

        public void OnClose()
        {
            _metaMessenger.Unsubscribe<ShowButtonConfirmUiMessage>(ShowButtonConfirmAsync);
            _metaMessenger.Unsubscribe<ShowPinUiMessage>(ShowPinAsync);
        }
    }
}
