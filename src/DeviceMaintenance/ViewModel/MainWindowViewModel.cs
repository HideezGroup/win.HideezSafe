using DeviceMaintenance.Messages;
using DeviceMaintenance.Service;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Connection;
using Hideez.SDK.Communication.Log;
using HideezMiddleware;
using HideezMiddleware.ConnectionModeProvider;
using HideezMiddleware.Modules.FwUpdateCheck;
using HideezMiddleware.Modules.FwUpdateCheck.Messages;
using HideezMiddleware.Threading;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;
using MvvmExtensions.Attributes;
using MvvmExtensions.PropertyChangedMonitoring;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DeviceMaintenance.ViewModel
{
    public class MainWindowViewModel : PropertyChangedImplementation
    {
        const string FW_FILE_EXTENSION = "img";
            
        readonly EventLogger _log;
        readonly MetaPubSub _hub;
        readonly FwUpdateCheckModule _fwUpdateCheckModule;

        bool _automaticallyUploadFirmware = Properties.Settings.Default.AutomaticallyUpload;
        bool _csrUpdate, _winBleUpdate, _isQuickUpdate, _isAdvancedUpdate, _isUpdateFromFileEnabled, _isAdvancedUpdateFromServerEnabled = true;
        string _fileName = Properties.Settings.Default.FirmwareFileName;
        string _cachedFilePath = string.Empty;
        DeviceModelInfo[] _deviceModelInfos = new DeviceModelInfo[0];

        readonly ConcurrentDictionary<string, DeviceViewModel> _devices =
            new ConcurrentDictionary<string, DeviceViewModel>();

        readonly ConcurrentDictionary<DeviceModelInfo, FwUpdateInfo[]> _fwVersions =
            new ConcurrentDictionary<DeviceModelInfo, FwUpdateInfo[]>();

        readonly SemaphoreQueue _deviceCreateSemaphore = new SemaphoreQueue(1, 1);
        private DeviceModelInfo _selectedModel;
        private FwUpdateInfo _selectedVersion;

        public IEnumerable<DeviceViewModel> Devices => _devices.Values.OrderByDescending(x => x.CreatedAt);
        public HideezServiceController HideezServiceController { get; }
        public ConnectionManagerViewModel ConnectionManager { get; }


        #region Properties

        public string Title
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly().GetName();
                return $"{assembly.Name} v{assembly.Version}";
            }
        }

        public string FirmwareFilePath
        {
            get
            {
                return _fileName;
            }
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    Properties.Settings.Default.FirmwareFileName = FirmwareFilePath;
                    Properties.Settings.Default.Save();
                    NotifyPropertyChanged();
                }
            }
        }

        public string CachedFilePath
        {
            get
            {
                return _cachedFilePath;
            }
            set
            {
                if (_cachedFilePath != value)
                {
                    _cachedFilePath = value;
                    NotifyPropertyChanged();
                }
            }
        }

        [DependsOn(nameof(CachedFilePath), nameof(FirmwareFilePath))]
        public string FilePath
        {
            get
            {
                return IsQuickUpdate ? CachedFilePath: FirmwareFilePath;
            }
        }

        [DependsOn(nameof(FirmwareFilePath))]
        public bool IsFirmwareSelected
        {
            get
            {
                return IsAdvancedUpdate && IsUpdateFromFileEnabled ? 
                    !string.IsNullOrWhiteSpace(FirmwareFilePath) && FirmwareFilePath.EndsWith($".{FW_FILE_EXTENSION}")
                    : true;
            }
        }

        [DependsOn(nameof(IsFirmwareSelected), nameof(IsCsrEnabled))]
        public bool IsTapEnabled
        {
            get
            {
                return IsFirmwareSelected && IsCsrEnabled;
            }
        }

        [DependsOn(nameof(IsFirmwareSelected), nameof(IsWinBleEnabled))]
        public bool IsWinBleUpdateAvailable
        {
            get
            {
                return IsFirmwareSelected && IsWinBleEnabled;
            }
        }

        public bool AutomaticallyUploadFirmware
        {
            get
            {
                return _automaticallyUploadFirmware;
            }
            set
            {
                if (_automaticallyUploadFirmware != value)
                {
                    _automaticallyUploadFirmware = value;
                    Properties.Settings.Default.AutomaticallyUpload = AutomaticallyUploadFirmware;
                    Properties.Settings.Default.Save();
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsCsrEnabled
        {
            get
            {
                return _csrUpdate;
            }
            set
            {
                if (_csrUpdate != value)
                {
                    if (value)
                    {
                        _devices.Clear();
                        NotifyPropertyChanged(nameof(Devices));
                        ConnectionManager.Initialize(DefaultConnectionIdProvider.Csr);
                    }
                    _csrUpdate = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsWinBleEnabled
        {
            get
            {
                return _winBleUpdate;
            }
            set
            {
                if (_winBleUpdate != value)
                {
                    if (value)
                    {
                        _devices.Clear();
                        NotifyPropertyChanged(nameof(Devices));
                        ConnectionManager.Initialize(DefaultConnectionIdProvider.WinBle);

                        AutomaticallyUploadFirmware = false;
                    }
                    _winBleUpdate = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsQuickUpdate
        {
            get
            {
                return _isQuickUpdate;
            }
            set
            {
                if (_isQuickUpdate != value)
                {
                    _isQuickUpdate = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsAdvancedUpdate
        {
            get
            {
                return _isAdvancedUpdate;
            }
            set
            {
                if (_isAdvancedUpdate != value)
                {
                    _isAdvancedUpdate = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsUpdateFromFileEnabled
        {
            get
            {
                return _isUpdateFromFileEnabled;
            }
            set
            {
                if (_isUpdateFromFileEnabled != value)
                {
                    _isUpdateFromFileEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool IsAdvancedUpdateFromServerEnabled
        {
            get
            {
                return _isAdvancedUpdateFromServerEnabled;
            }
            set
            {
                if (_isAdvancedUpdateFromServerEnabled != value)
                {
                    _isAdvancedUpdateFromServerEnabled = value;
                    NotifyPropertyChanged();
                }
            }
        }

        [DependsOn(nameof(IsAdvancedUpdate), nameof(IsQuickUpdate))]
        public bool IsButtonsVisible
        {
            get => !IsAdvancedUpdate && !IsQuickUpdate;
        }

        /// <summary>
        /// Returns true if any device is currently undergoing firmware update
        /// </summary>
        [DependsOn(nameof(Devices))]
        public bool IsFirmwareUpdateInProgress
        {
            get
            {
                return Devices.Any(d => d.InProgress);
            }
        }

        public DeviceModelInfo[] DeviceModels
        {
            get
            {
                return _deviceModelInfos; 
            }
            set
            {
                _deviceModelInfos = value;
                NotifyPropertyChanged();
            }
        }

        public DeviceModelInfo SelectedModel
        {
            get
            {
                return _selectedModel;
            }
            set
            {
                if (_selectedModel != value)
                {
                    _selectedModel = value;
                    NotifyPropertyChanged();
                }
            }
        }

        [DependsOn(nameof(SelectedModel))]
        public FwUpdateInfo[] Versions
        {
            get
            {
                if (SelectedModel != null)
                    return _fwVersions[SelectedModel];
                else 
                    return new FwUpdateInfo[0];
            }
        }

        public FwUpdateInfo SelectedVersion
        {
            get
            {
                return _selectedVersion;
            }
            set
            {
                if(_selectedVersion != value)
                {
                    _selectedVersion = value;
                    NotifyPropertyChanged();
                }
            }
        }
        #endregion

        #region Commands

        public ICommand SelectFirmwareCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (x) =>
                    {
                        SelectFirmware();
                    }
                };
            }
        }

        public ICommand QuickUpdateCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (x) =>
                    {
                        IsQuickUpdate = true;
                    }
                };
            }
        }

        public ICommand AdvancedUpdateCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (x) =>
                    {
                        IsAdvancedUpdate = true;
                    }
                };
            }
        }

        public ICommand GoToStartPageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (x) =>
                    {
                        IsAdvancedUpdate = false;
                        IsQuickUpdate = false;
                    }
                };
            }
        }

        #endregion

        public MainWindowViewModel(MetaPubSub hub)
        {
            // default value -33 is to much and picks up devices from very far
            SdkConfig.TapProximityUnlockThreshold = -29;
            SdkConfig.ConnectDeviceTimeout = 8_000;
            SdkConfig.DeviceInitializationTimeout = 5_000;

            _hub = hub;
            _log = new EventLogger("Maintenance");

            _hub.Subscribe<AdvertismentReceivedEvent>(OnAdvertismentReceived);
            _hub.Subscribe<ControllerAddedEvent>(OnControllerAdded);
            _hub.Subscribe<DeviceConnectedEvent>(OnDeviceConnected);
            _hub.Subscribe<ClosingEvent>(OnClosing);
            _hub.Subscribe<AvailableDeviceModelsMessage>(OnAvailableDeviceModelsReceived);
            _hub.Subscribe<FilePathDetectionMessage>(OnFilePathDetection);

            _fwUpdateCheckModule = new FwUpdateCheckModule(HideezClientRegistryRoot.GetRootRegistryKey(true), _hub, _log);
            ConnectionModeProvider modeProvider = new ConnectionModeProvider(HideezClientRegistryRoot.GetRootRegistryKey(false), _log);

            ConnectionManager = new ConnectionManagerViewModel(_log, _hub);
            HideezServiceController = new HideezServiceController(_log, _hub);

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

            if (modeProvider.IsCsrMode)
                IsCsrEnabled = true;
            else
                IsWinBleEnabled = true;
        }

        Task OnClosing(ClosingEvent arg)
        {
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            return Task.CompletedTask;
        }

        // WinBle only stuff
        async Task OnControllerAdded(ControllerAddedEvent arg)
        {
            await CreateDeviceViewModel(arg.ConnectionId);
        }

        // CSR only stuff
        async Task OnAdvertismentReceived(AdvertismentReceivedEvent arg)
        {
            var connectionId = new ConnectionId(arg.DeviceId, (byte)DefaultConnectionIdProvider.Csr);
            await CreateDeviceViewModel(connectionId);
        }

        private Task OnAvailableDeviceModelsReceived(AvailableDeviceModelsMessage arg)
        {
            DeviceModels = arg.AvailableModels;

            Task.Run(InitializeModelsVersions);

            return Task.CompletedTask;
        }

        private async Task InitializeModelsVersions()
        {
            foreach (var model in DeviceModels)
            {
                var response = await _hub.Process<GetFwUpdatesCollectionResponse>(new GetFwUpdatesCollectionMessage(model.Code));
                FwUpdateInfo[] fwUpdates = response.FwUpdatesInfo;

                _fwVersions.TryAdd(model, fwUpdates);
            }
        }

        private async Task OnFilePathDetection(FilePathDetectionMessage arg)
        {
            string filePath = string.Empty;
            try
            {

                if (IsQuickUpdate)
                {
                    var deviceModel = GetDeviceModelBySerialNo(arg.DeviceSerialNo);
                    var response = await _hub.Process<GetFwUpdateByDeviceModelResponse>(new GetFwUpdateByDeviceModelMessage(deviceModel.Code));
                    filePath = response.FilePath;
                }
                else if (IsAdvancedUpdate)
                {
                    if (IsAdvancedUpdateFromServerEnabled)
                    {
                        if (SelectedVersion != null)
                        {
                            var response = await _hub.Process<GetFwUpdateFilePathResponse>(new GetFwUpdateFilePathMessage(SelectedVersion));
                            filePath = response.FilePath;
                        }
                    }
                    else if (IsUpdateFromFileEnabled)
                        filePath = FirmwareFilePath;
                }
            }
            finally
            {
                await _hub.Publish(new FilePathDetectionResponse(filePath));
            }
        }

        async Task CreateDeviceViewModel(ConnectionId connectionId)
        {
            await _deviceCreateSemaphore.WaitAsync();
            DeviceViewModel deviceViewModel = null;
            try
            {
                bool added = false;
                deviceViewModel = _devices.GetOrAdd(connectionId.Id, (id) =>
                {
                    added = true;
                    bool isBonded = ConnectionManager.IsBonded(id);
                    return new DeviceViewModel(connectionId, isBonded, _hub);
                });

                deviceViewModel.PropertyChanged += DeviceViewModel_PropertyChanged;

                if (added)
                {
                    NotifyPropertyChanged(nameof(Devices));
                }

                await deviceViewModel.TryConnect();
            }
            catch (Exception ex)
            {
                if (deviceViewModel != null)
                    deviceViewModel.CustomError = ex.Message;
            }
            finally
            {
                _deviceCreateSemaphore.Release();
            }
        }

        private void DeviceViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "InProgress")
                NotifyPropertyChanged(nameof(IsFirmwareUpdateInProgress));
        }

        Task OnDeviceConnected(DeviceConnectedEvent arg)
        {
            if (IsFirmwareSelected && (AutomaticallyUploadFirmware || arg.DeviceViewModel.IsBoot))
                return arg.DeviceViewModel.StartFirmwareUpdate();

            return Task.CompletedTask;
        }

        void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock ||
                e.Reason == SessionSwitchReason.SessionLogoff ||
                e.Reason == SessionSwitchReason.SessionUnlock ||
                e.Reason == SessionSwitchReason.SessionLogon)
            {
                AutomaticallyUploadFirmware = false;
            }
        }

        void SelectFirmware()
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                InitialDirectory = string.IsNullOrWhiteSpace(FirmwareFilePath) ? string.Empty : Path.GetDirectoryName(FirmwareFilePath),
                Filter = "Firmware Image file | *.img"
            };

            if (ofd.ShowDialog() == true)
                FirmwareFilePath = ofd.FileName;
        }


        DeviceModelInfo GetDeviceModelBySerialNo(string serialNo)
        {
            foreach (var deviceModel in _deviceModelInfos)
            {
                if (serialNo.StartsWith(deviceModel.Name))
                    return deviceModel;
            }

            return null;
        }
    }
}
