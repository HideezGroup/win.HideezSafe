using DeviceMaintenance.Messages;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.LongOperations;
using HideezMiddleware.Localize;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Attributes;
using MvvmExtensions.PropertyChangedMonitoring;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DeviceMaintenance.ViewModel
{
    public class DeviceViewModel : PropertyChangedImplementation
    {
        //  None -> Bonding|Connecting -> Connected -> EnteringBoot -> Updating|Wiping -> Success|Error
        public enum State
        {
            None,
            Bonding,
            Connecting,
            Connected,
            EnteringBoot,
            Updating,
            Wiping,
            Success,
            Error
        }

        readonly object _stateLocker = new object();
        readonly MetaPubSub _hub;
        readonly ConnectionId _connectionId;
        readonly bool _isBonded;
        readonly LongOperation _longOperation = new LongOperation(1);

        IDevice _device = null;
        string _customError = string.Empty;
        State _state;


        public IDevice Device => _device;
        public DateTime CreatedAt = DateTime.Now;
        public bool IsConnected => _device?.IsConnected ?? false;
        public bool IsBoot => _device?.IsBoot ?? false;
        public string SerialNo => _device?.SerialNo != null ? $"{_device.SerialNo} (v{_device.FirmwareVersion}, b{_device.BootloaderVersion})" : _connectionId.DeviceName;

        public double Progress => _longOperation.Progress;
        public bool InProgress => _longOperation.IsRunning;

        public string CustomError
        {
            get
            {
                return _customError;
            }
            set
            {
                if (_customError != value)
                {
                    _customError = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public State CurrentState
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
                NotifyPropertyChanged();
            }
        }


        #region Visual States
        [DependsOn(nameof(CurrentState))]
        public bool BondingState => CurrentState == State.Bonding;

        [DependsOn(nameof(CurrentState))]
        public bool ConnectingState => CurrentState == State.Connecting;

        [DependsOn(nameof(CurrentState))]
        public bool ReadyToUpdateState => CurrentState == State.Connected;

        [DependsOn(nameof(CurrentState))]
        public bool EnteringBootModeState => CurrentState == State.EnteringBoot;

        [DependsOn(nameof(CurrentState))]
        public bool UpdatingState => CurrentState == State.Updating;

        [DependsOn(nameof(CurrentState))]
        public bool WipingState => CurrentState == State.Wiping;

        [DependsOn(nameof(CurrentState))]
        public bool SuccessState => CurrentState == State.Success;

        [DependsOn(nameof(CurrentState))]
        public bool ErrorState => CurrentState == State.Error;
        #endregion

        public ICommand UpdateDevice
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = async (x) =>
                    {
                        await StartFirmwareUpdate();
                    }
                };
            }
        }

        public ICommand WipeDevice
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = async (x) =>
                    {
                        await OnWipeDevice();
                    }
                };
            }
        }


        public DeviceViewModel(ConnectionId connectionId, bool isBonded, MetaPubSub hub)
        {
            RegisterDependencies();

            _hub = hub;
            _connectionId = connectionId;
            _isBonded = isBonded;

            _longOperation.StateChanged += (object sender, EventArgs e) =>
            {
                NotifyPropertyChanged(nameof(InProgress));
                NotifyPropertyChanged(nameof(Progress));
            };
        }

        public void SetDevice(IDevice device)
        {
            _device = device;

            _device.ConnectionStateChanged += (object sender, EventArgs e) =>
            {
                try
                {
                    if (!IsConnected && CurrentState == State.Wiping)
                    {
                        CurrentState = State.Success;
                        _hub.Publish(new DeviceWipedEvent(this));
                    }

                    NotifyPropertyChanged(nameof(IsConnected));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            };

            _device.PropertyChanged += (object sender, string e) =>
            {
                NotifyPropertyChanged(e);
            };

            NotifyPropertyChanged(nameof(SerialNo));
        }

        //  None -> Bonding|Connecting -> Connected -> EnteringBoot -> Updating -> Success
        internal async Task TryConnect()
        {
            lock (_stateLocker)
            {
                if (CurrentState != State.None && CurrentState != State.Error)
                    return;
                CurrentState = _isBonded ? State.Connecting : State.Bonding;
            }

            try
            {
                var res = await _hub.Process<ConnectDeviceResponse>(
                    new ConnectDeviceCommand(_connectionId),
                    x => x.ConnectionId == _connectionId);

                if (res.Device == null)
                {
                    if(_connectionId.IdProvider == (byte)DefaultConnectionIdProvider.WinBle)
                        throw new Exception(TranslationSource.Instance["Error.FailedConnection.WinBle"]);
                    else if(_connectionId.IdProvider == (byte)DefaultConnectionIdProvider.Csr)
                        throw new Exception(TranslationSource.Instance["Error.FailedConnection.Csr"]);
                }

                CurrentState = State.Connected;
                SetDevice(res.Device);

                //not awaiting, because if device is in boot updating will start immediately 
                //and block connection of other devices.
                _hub.Publish(new DeviceConnectedEvent(this));
            }
            catch (Exception ex)
            {
                CustomError = ex.FlattenMessage();
                CurrentState = State.Error;
            }
        }

        public async Task StartFirmwareUpdate()
        {
            try
            {
                var response = await _hub.Process<FilePathDetectionResponse>(new FilePathDetectionMessage(SerialNo));
                string filePath = response.FilePath;

                if (string.IsNullOrWhiteSpace(filePath))
                    throw new Exception(TranslationSource.Instance["Error.NoAvailableFw"]);

                CurrentState = State.EnteringBoot;

                var res = await _hub.Process<EnterBootResponse>(
                    new EnterBootCommand(this, filePath, _device, _longOperation), r => r.DeviceId == _device.Id);

                CurrentState = State.Updating;

                await res.ImageUploader.Run(false);

                CurrentState = State.Success;
            }
            catch (Exception ex)
            {
                CustomError = ex.FlattenMessage();
                CurrentState = State.Error;
            }
        }


        async Task OnWipeDevice()
        {
            try
            {
                var mb = MessageBox.Show(TranslationSource.Instance["Wipe.Text"],
                    TranslationSource.Instance["Wipe.Title"],
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Exclamation);

                if (mb == MessageBoxResult.OK)
                {
                    await _device.Wipe(Encoding.UTF8.GetBytes(""));
                    CurrentState = State.Wiping;
                    await Task.Delay(30_000);
                    if (CurrentState != State.Success)
                    {
                        if (IsConnected)
                            CurrentState = State.Connected;
                        else
                            CurrentState = State.Error;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

    }
}
