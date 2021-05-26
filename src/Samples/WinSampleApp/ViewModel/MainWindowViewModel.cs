using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Hideez.CsrBLE;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.FW;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.LongOperations;
using Hideez.SDK.Communication.PasswordManager;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Utils;
using HideezMiddleware;
using HideezMiddleware.DeviceConnection;
using HideezMiddleware.DeviceConnection.Workflow;
using HideezMiddleware.Settings;
using HideezMiddleware.Workstation;
using Microsoft.Win32;
using Hideez.SDK.Communication.Connection;
using HideezMiddleware.CredentialProvider;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using Meta.Lib.Modules.PubSub;
using HideezMiddleware.Utils;
using Hideez.SDK.Communication.Backup;
using System.Threading;
using HideezMiddleware.DeviceConnection.ConnectionProcessors.Dongle;
using HideezMiddleware.ConnectionModeProvider;

namespace WinSampleApp.ViewModel
{
    public class MainWindowViewModel : ViewModelBase, IClientUiProxy, IWorkstationUnlocker
    {
        readonly EventLogger _log;
        readonly IMetaPubSub _messenger;
        readonly IBleConnectionManager _csrConnectionManager;
        readonly DeviceManager _deviceManager;
        readonly CredentialProviderProxy _credentialProviderProxy;
        readonly ConnectionFlowProcessorBase _connectionFlowProcessor;
        readonly TapConnectionProcessor _tapProcessor;
        readonly HesAppConnection _hesConnection;

        byte _nextChannelNo = 2;

        public AccessParams AccessParams { get; set; }

        public string PrimaryAccountLogin { get; set; }
        public string PrimaryAccountPassword { get; set; }

        public string Pin { get; set; }
        public string OldPin { get; set; }
        public string CODE { get; set; }
        public string Passphrase { get; set; }
        public string BleAdapterState => _csrConnectionManager?.State.ToString();

        public string ConectByMacAddress1
        {
            get { return Properties.Settings.Default.DefaultMac; }
            set
            {
                Properties.Settings.Default.DefaultMac = value;
                Properties.Settings.Default.Save();
            }
        }

        public string ConectByMacAddress2
        {
            get { return Properties.Settings.Default.DefaultMac2; }
            set
            {
                Properties.Settings.Default.DefaultMac2 = value;
                Properties.Settings.Default.Save();
            }
        }

        public string RfidAdapterState => "NA";
        public string RfidAddress { get; set; }

        public string HesAddress
        {
            get
            {
                return string.IsNullOrWhiteSpace(Properties.Settings.Default.DefaultHesAddress) ? 
                    "https://localhost:44371" : Properties.Settings.Default.DefaultHesAddress;
            }
            set
            {
                Properties.Settings.Default.DefaultHesAddress = value;
                Properties.Settings.Default.Save();
            }
        }

        public HesConnectionState HesState => _hesConnection.State;

        public string LicenseText { get; set; }


        #region Properties

        string _clientUiStatus;
        public string ClientUiStatus
        {
            get
            {
                return _clientUiStatus;
            }
            set
            {
                if (_clientUiStatus != value)
                {
                    _clientUiStatus = value;
                    NotifyPropertyChanged(nameof(ClientUiStatus));
                }
            }
        }

        string _clientUiNotification;
        public string ClientUiNotification
        {
            get
            {
                return _clientUiNotification;
            }
            set
            {
                if (_clientUiNotification != value)
                {
                    _clientUiNotification = value;
                    NotifyPropertyChanged(nameof(ClientUiNotification));
                }
            }
        }

        string _clientUiError;
        public string ClientUiError
        {
            get
            {
                return _clientUiError;
            }
            set
            {
                if (_clientUiError != value)
                {
                    _clientUiError = value;
                    NotifyPropertyChanged(nameof(ClientUiError));
                }
            }
        }

        bool bleAdapterDiscovering;
        public bool BleAdapterDiscovering
        {
            get
            {
                return bleAdapterDiscovering;
            }
            set
            {
                if (bleAdapterDiscovering != value)
                {
                    bleAdapterDiscovering = value;
                    NotifyPropertyChanged(nameof(BleAdapterDiscovering));
                }
            }
        }

        DeviceViewModel currentDevice;
        public DeviceViewModel CurrentDevice
        {
            get
            {
                return currentDevice;
            }
            set
            {
                if (currentDevice != value)
                {
                    currentDevice = value;
                    NotifyPropertyChanged(nameof(CurrentDevice));
                }
            }
        }

        DiscoveredDeviceAddedEventArgs currentDiscoveredDevice;
        private GetPinWindow _getPinWindow;

        public DiscoveredDeviceAddedEventArgs CurrentDiscoveredDevice
        {
            get
            {
                return currentDiscoveredDevice;
            }
            set
            {
                if (currentDiscoveredDevice != value)
                {
                    currentDiscoveredDevice = value;
                    NotifyPropertyChanged(nameof(CurrentDiscoveredDevice));
                }
            }
        }

        public ObservableCollection<DiscoveredDeviceAddedEventArgs> DiscoveredDevices { get; }
            = new ObservableCollection<DiscoveredDeviceAddedEventArgs>();


        public ObservableCollection<DeviceViewModel> Devices { get; }
            = new ObservableCollection<DeviceViewModel>();

        string backupPasswordText = "12345678";
        public string BackupPasswordText
        {
            get 
            { 
                return backupPasswordText; 
            }
            set
            {
                if (backupPasswordText != value)
                {
                    backupPasswordText = value;
                    NotifyPropertyChanged(nameof(BackupPasswordText));
                }
            }
        }
        #endregion Properties


        #region Commands

        public ICommand CancelConnectionFlowCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        CancelConnectionFlow(CurrentDevice);
                    }
                };
            }
        }

        public ICommand SetPinCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = async (x) =>
                    {
                        await SetPin(CurrentDevice);
                    }
                };
            }
        }

        public ICommand ForceSetPinCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = async (x) =>
                    {
                        await ForceSetPin(CurrentDevice);
                    }
                };
            }
        }

        public ICommand EnterPinCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = EnterPin(CurrentDevice);
                    }
                };
            }
        }

        public ICommand CheckPassphraseCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = CheckPassphrase(CurrentDevice);
                    }
                };
            }
        }

        public ICommand LinkDeviceCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = LinkDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand AccessDeviceCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = AccessDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand WipeDeviceCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = WipeDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand WipeDeviceManualCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = WipeDeviceManual(CurrentDevice);
                    }
                };
            }
        }

        public ICommand UnlockDeviceCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = UnlockDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand ConnectHesCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return true;
                    },
                    CommandAction = (x) =>
                    {
                        ConnectHes();
                    }
                };
            }
        }

        public ICommand DisconnectHesCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return true;
                    },
                    CommandAction = async (x) =>
                    {
                        await DisconnectHes();
                    }
                };
            }
        }

        public ICommand UnlockByRfidCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return true;
                    },
                    CommandAction = (x) =>
                    {
                        UnlockByRfid();
                    }
                };
            }
        }


        public ICommand BleAdapterResetCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return true;
                    },
                    CommandAction = (x) =>
                    {
                        ResetBleAdapter();
                    }
                };
            }
        }

        public ICommand StartDiscoveryCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return _csrConnectionManager.State == BluetoothAdapterState.PoweredOn && !BleAdapterDiscovering;
                    },
                    CommandAction = (x) =>
                    {
                        StartDiscovery();
                    }
                };
            }
        }

        public ICommand StopDiscoveryCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return _csrConnectionManager.State == BluetoothAdapterState.PoweredOn && BleAdapterDiscovering;
                    },
                    CommandAction = (x) =>
                    {
                        StopDiscovery();
                    }
                };
            }
        }

        public ICommand ClearDiscoveredDeviceListCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return true;
                    },
                    CommandAction = (x) =>
                    {
                        ClearDiscoveredDeviceList();
                    }
                };
            }
        }

        public ICommand RemoveAllDevicesCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return Devices.Count > 0;
                    },
                    CommandAction = (x) =>
                    {
                        RemoveAllDevices();
                    }
                };
            }
        }

        public ICommand ConnectDiscoveredDeviceCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDiscoveredDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        ConnectDiscoveredDevice(CurrentDiscoveredDevice);
                    }
                };
            }
        }

        public ICommand ConnectByMacCommand1
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return true;
                    },
                    CommandAction = async (x) =>
                    {
                        await ConnectDeviceByMac(ConectByMacAddress1);
                    }
                };
            }
        }

        public ICommand ConnectByMacCommand2
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return true;
                    },
                    CommandAction = async (x) =>
                    {
                        await ConnectDeviceByMac(ConectByMacAddress2);
                    }
                };
            }
        }

        public ICommand SyncDevicesCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        SyncDevices();
                    }
                };
            }
        }

        public ICommand ConnectDeviceCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        ConnectDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand DisconnectDeviceCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = DisconnectDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand PingDeviceCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = PingDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand VerifyAndInitializeDeviceCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;  
                    },
                    CommandAction = (x) =>
                    {
                        _ = VerifyAndInitializeDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand AddDeviceChannelCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        AddDeviceChannel(CurrentDevice);
                    }
                };
            }
        }

        public ICommand RemoveDeviceChannelCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        RemoveDeviceChannel(CurrentDevice);
                    }
                };
            }
        }

        public ICommand Test1Command
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        Test();
                    }
                };
            }
        }

        public ICommand WritePrimaryAccountCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = WritePrimaryAccount(CurrentDevice);
                    }
                };
            }
        }

        public ICommand DeviceInfoCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = DeviceInfo(CurrentDevice);
                    }
                };
            }
        }

        public ICommand ConfirmCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = OnConfirmAsync(CurrentDevice);
                    }
                };
            }
        }

        public ICommand GetOtpCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = OnGetOtpAsync(CurrentDevice);
                    }
                };
            }
        }

        public ICommand StorageCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        OpenStorageVindow(CurrentDevice);
                    }
                };
            }
        }

        public ICommand LoadLicenseCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = LoadLicense(CurrentDevice, 0, LicenseText);
                    }
                };
            }
        }

        public ICommand LoadLicenseIntoEmptyCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = LoadLicense(CurrentDevice, LicenseText);
                    }
                };
            }
        }
        public ICommand QueryLicenseCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = QueryLicense(CurrentDevice, 0);
                    }
                };
            }
        }

        public ICommand QueryAllLicensesCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = QueryAllLicenses(CurrentDevice);
                    }
                };
            }
        }

        public ICommand QueryActiveLicenseCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = QueryActiveLicense(CurrentDevice);
                    }
                };
            }
        }

        public ICommand FetchLogCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = FetchDeviceLog(CurrentDevice);
                    }
                };
            }
        }

        public ICommand ClearLogCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = ClearDeviceLog(CurrentDevice);
                    }
                };
            }
        }
        //
        public ICommand LockDeviceCodeCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = LockDeviceCode(CurrentDevice);
                    }
                };
            }
        }
        public ICommand UnlockDeviceCodeCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = UnlockDeviceCode(CurrentDevice);
                    }
                };
            }
        }

        public ICommand BackupCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = RunDeviceBackupProcedure(CurrentDevice);
                    }
                };
            }
        }

        public ICommand RestoreCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null;
                    },
                    CommandAction = (x) =>
                    {
                        _ = RunDeviceRestoreProcedure(CurrentDevice);
                    }
                };
            }
        }

        #endregion

        public MainWindowViewModel()
        {
            try
            {
                // Required to properly handle session switch event in any environment
                SystemEvents.SessionSwitch += (sender, args) =>
                {
                    SessionSwitchMonitor.SystemSessionSwitch(Process.GetCurrentProcess().SessionId, args.Reason);
                };

                AccessParams = new AccessParams()
                {
                    MasterKey_Bond = true,
                    MasterKey_Connect = false,
                    //MasterKey_Link = false,
                    MasterKey_Channel = false,

                    Button_Bond = false,
                    Button_Connect = false,
                    //Button_Link = true,
                    Button_Channel = true,

                    Pin_Bond = false,
                    Pin_Connect = true,
                    //Pin_Link = false,
                    Pin_Channel = false,

                    PinMinLength = 4,
                    PinMaxTries = 3,
                    MasterKeyExpirationPeriod = 0,
                    PinExpirationPeriod = 15 * 60,
                    ButtonExpirationPeriod = 0,
                };

                _log = new EventLogger("ExampleApp");

                CODE = "123456";

                // BleConnectionManager ============================
                var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var bondsFolderPath = $"{commonAppData}\\Hideez\\Service\\Bonds";

                _csrConnectionManager = new BleConnectionManager(_log, bondsFolderPath);
                _csrConnectionManager.AdapterStateChanged += ConnectionManager_AdapterStateChanged;
                _csrConnectionManager.DiscoveryStopped += ConnectionManager_DiscoveryStopped;
                _csrConnectionManager.DiscoveredDeviceAdded += ConnectionManager_DiscoveredDeviceAdded;
                _csrConnectionManager.DiscoveredDeviceRemoved += ConnectionManager_DiscoveredDeviceRemoved;

                var coordinator = new ConnectionManagersCoordinator();
                coordinator.AddConnectionManager(_csrConnectionManager);

                _deviceManager = new DeviceManager(coordinator, _log);

                _deviceManager.DeviceAdded += DeviceManager_DeviceAdded;
                _deviceManager.DeviceRemoved += DeviceManager_DeviceRemoved;

                // WorkstationInfoProvider ==================================
                var clientRegistryRoot = HideezClientRegistryRoot.GetRootRegistryKey(true);
                var workstationIdProvider = new WorkstationIdProvider(clientRegistryRoot, _log);
                var workstationInfoProvider = new WorkstationInfoProvider(workstationIdProvider, _log); //todo - HesAddress?

                // HES Connection ==================================

                // do not use in the production!
                //string hesAddress = @"https://192.168.10.249/";
                //ServicePointManager.ServerCertificateValidationCallback +=
                //(sender, cert, chain, error) =>
                //{
                //    if (sender is HttpWebRequest request)
                //    {
                //        if (request.Address.AbsoluteUri.StartsWith(hesAddress))
                //            return true;
                //    }
                //    return error == SslPolicyErrors.None;
                //};

                string workstationId = Guid.NewGuid().ToString();
                _hesConnection = new HesAppConnection(workstationInfoProvider, _log);
                _hesConnection.HubConnectionStateChanged += (sender, e) => NotifyPropertyChanged(nameof(HesState));
                //_hesConnection.Start(HesAddress);

                // Credential provider ==============================
                // Todo: Should be connection mode agnostic
                ConnectionModeProvider connectionModeProvider = new ConnectionModeProvider(clientRegistryRoot, _log);
                _credentialProviderProxy = new CredentialProviderProxy(connectionModeProvider, _log);
                _credentialProviderProxy.Start();

                // Service Settings Manager ==================================
                var serviceSettingsManager = new SettingsManager<ServiceSettings>(string.Empty, new XmlFileSerializer(_log));

                // Unlocker Settings Manager ==================================
                var proximitySettingsManager = new SettingsManager<ProximitySettings>(string.Empty, new XmlFileSerializer(_log));

                // Rfid Settings Manager =========================
                var rfidSettingsManager = new SettingsManager<RfidSettings>(string.Empty, new XmlFileSerializer(_log));

                // UI proxy ==================================
                var uiProxyManager = new UiProxyManager(_credentialProviderProxy, this, _log);

                // ConnectionFlowProcessor ==================================
                var hesAccessManager = new HesAccessManager(clientRegistryRoot, _log);
                var bondManager = new BondManager(bondsFolderPath, _log);
                var connectionFlowProcessorfactory = new StandaloneConnectionFlowProcessorFactory(
                    _deviceManager,
                    bondManager,
                    _credentialProviderProxy,
                    null,
                    uiProxyManager,
                    null,
                    null,
                    null,
                    _messenger,
                    _log);
                _connectionFlowProcessor = connectionFlowProcessorfactory.Create();

                _tapProcessor = new TapConnectionProcessor(_connectionFlowProcessor, _csrConnectionManager, _messenger, _log);
                _tapProcessor.Start();

                _csrConnectionManager.Start();

                ClientConnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ClientUiError = ex.Message;
            }
        }

        void ConnectHes()
        {
            try
            {
                _hesConnection.Start(HesAddress);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task DisconnectHes()
        {
            try
            {
                await _hesConnection.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        internal async Task Close()
        {
            await _hesConnection.Stop();
            _credentialProviderProxy?.Stop();
        }

        void DeviceManager_DeviceAdded(object sender, DeviceAddedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                    var deviceViewModel = new DeviceViewModel(e.Device);
                    Devices.Add(deviceViewModel);
                    if (CurrentDevice == null)
                        CurrentDevice = deviceViewModel;
            });
        }

        void DeviceManager_DeviceRemoved(object sender, DeviceRemovedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var item = Devices.FirstOrDefault(x => x.Id == e.Device.Id && 
                                                    x.ChannelNo == e.Device.ChannelNo);

                if (item != null)
                    Devices.Remove(item);
            });
        }

        void ConnectionManager_DiscoveredDeviceAdded(object sender, DiscoveredDeviceAddedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                DiscoveredDevices.Add(e);
            });
        }

        void ConnectionManager_DiscoveredDeviceRemoved(object sender, DiscoveredDeviceRemovedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var item = DiscoveredDevices.FirstOrDefault(x => x.Id == e.Id);
                if (item != null)
                    DiscoveredDevices.Remove(item);
            });
        }

        void ConnectionManager_DiscoveryStopped(object sender, EventArgs e)
        {
            BleAdapterDiscovering = false;
        }

        void ConnectionManager_AdapterStateChanged(object sender, EventArgs e)
        {
            NotifyPropertyChanged(nameof(BleAdapterState));
        }

        void ResetBleAdapter()
        {
            _csrConnectionManager.Restart();
        }

        void StartDiscovery()
        {
            //DiscoveredDevices.Clear();
            _csrConnectionManager.Start();
            BleAdapterDiscovering = true;
        }

        void StopDiscovery()
        {
            _csrConnectionManager.Stop();
            BleAdapterDiscovering = false;
            DiscoveredDevices.Clear();
        }

        void ClearDiscoveredDeviceList()
        {
            DiscoveredDevices.Clear();
        }

        async void RemoveAllDevices()
        {
            try
            {
                foreach (var device in _deviceManager.Devices.Where(d => d.ChannelNo == (byte)DefaultDeviceChannel.Main))
                    await _deviceManager.DeleteBond(device.DeviceConnection);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void ConnectDiscoveredDevice(DiscoveredDeviceAddedEventArgs e)
        {
            await _deviceManager.Connect(new ConnectionId(e.Id, (byte)DefaultConnectionIdProvider.Csr));
        }

        async Task ConnectDeviceByMac(string mac)
        {
            try
            {
                _log.WriteLine("MainVM", $"Waiting Device connection {mac} ..........................");

                var device = await _deviceManager.Connect(new ConnectionId(mac, (byte)DefaultConnectionIdProvider.Csr)).TimeoutAfter(SdkConfig.ConnectDeviceTimeout);

                if (device != null)
                    _log.WriteLine("MainVM", $"Device connected {device.Name} ++++++++++++++++++++++++");
                else
                    _log.WriteLine("MainVM", "Device NOT connected --------------------------");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void ConnectDevice(DeviceViewModel device)
        {
            try
            {
                await _deviceManager.Connect(device.Device.DeviceConnection.Connection.ConnectionId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task DisconnectDevice(DeviceViewModel device)
        {
            try
            {
                await _deviceManager.Disconnect(device.Device.DeviceConnection);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task PingDevice(DeviceViewModel device)
        {
            try
            {
                //var pingText = $"{device.Id} {DateTime.Now} qwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqer";
                var pingText = $"{device.Id} {DateTime.Now}";
                //pingText = pingText + pingText + pingText;
                var reply = await device.Device.Ping(pingText);
                if (pingText != reply.ResultAsString)
                    throw new Exception("Wrong reply: " + reply.ResultAsString);
                else
                    MessageBox.Show(reply.ResultAsString);
            }
            catch (Exception ex)
            {
                MessageBox.Show(HideezExceptionLocalization.GetErrorAsString(ex));
            }
        }

        async Task VerifyAndInitializeDevice(DeviceViewModel device)
        {
            try
            {
                await device.Device.VerifyAndInitialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show(HideezExceptionLocalization.GetErrorAsString(ex));
            }
        }

        async Task WritePrimaryAccount(DeviceViewModel device)
        {
            try
            {
                var pm = new DevicePasswordManager(device.Device, _log);

                var account = new AccountRecord
                {
                    Key = 1,
                    StorageId = new StorageId(new byte[] { 0 }),
                    Timestamp = 0,
                    Name = "My Primary Account",
                    Login = PrimaryAccountLogin,
                    Password = PrimaryAccountPassword,
                    OtpSecret = null,
                    Apps = null,
                    Urls = null,
                    IsPrimary = true
                };

                await pm.SaveOrUpdateAccount(
                    account.StorageId, account.Timestamp, account.Name,
                    account.Password, account.Login, account.OtpSecret,
                    account.Apps, account.Urls,
                    account.IsPrimary
                    );
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void AddDeviceChannel(DeviceViewModel currentDevice)
        {
            var newDevice = _deviceManager.AddDeviceChannel(currentDevice.Device, _nextChannelNo++);
            Task.Run(newDevice.VerifyAndInitialize);
        }

        void RemoveDeviceChannel(DeviceViewModel currentDevice)
        {
            _deviceManager.RemoveDeviceChannel(currentDevice.Device);
            _nextChannelNo--;
        }

        void Test()
        {
            try
            {
                foreach (var device in _deviceManager.Devices)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            for (int i = 0; i < 10000; i++)
                            {
                                var pingText = $"{device.Id}_{i}";
                                //for (int j = 0; j < 6; j++)
                                //    pingText += pingText;
                                var reply = await device.Ping(pingText, 20_000);
                                if (pingText != reply.ResultAsString)
                                    throw new Exception("Wrong reply");
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    });

                    Task.Run(async () =>
                    {
                        try
                        {
                            //await Task.Delay(1000);
                            for (int i = 0; i < 10000; i++)
                            {
                                var pingText = $"{device.Id}_{i + 5}";
                                var reply = await device.Ping(pingText);
                                if (pingText != reply.ResultAsString)
                                    throw new Exception("Wrong reply");
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        
        async Task FetchDeviceLog(DeviceViewModel device)
        {
            try
            {
                var deviceLog = await device.Device.FetchLog();
                if (deviceLog.Length > 0)
                {
                    var logEntries = DeviceLogParser.ParseLog(deviceLog);
                    var sb = new StringBuilder();
                    foreach (var entry in logEntries)
                        sb.Append(entry.ToString() + Environment.NewLine);
                    var str = sb.ToString();
                    Clipboard.SetText(str);
                    MessageBox.Show($"Log copied into clipboard ({str.Length} chars)");
                }
                else
                {
                    MessageBox.Show("No eventlog data recorded\n");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(HideezExceptionLocalization.GetErrorAsString(ex));
            }
        }

        async Task ClearDeviceLog(DeviceViewModel device)
        {
            try
            {
                await device.Device.ClearLog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(HideezExceptionLocalization.GetErrorAsString(ex));
            }
        }

        //LockDeviceCode
        async Task LockDeviceCode(DeviceViewModel device)
        {
            try
            {
                byte[] code = Encoding.UTF8.GetBytes(CODE);
                byte[] key= MasterPasswordConverter.GetMasterKey(Passphrase, device.SerialNo);
                byte unlockAttempts = 5;// Options 3-15
                await device.Device.LockDeviceCode(key, code, unlockAttempts);
                await device.Device.RefreshDeviceInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(HideezExceptionLocalization.GetErrorAsString(ex));
            }
        }

        async Task UnlockDeviceCode(DeviceViewModel device)
        {
            try
            {
                byte[] code = Encoding.UTF8.GetBytes(CODE);
                await device.Device.UnlockDeviceCode(code);
                await device.Device.RefreshDeviceInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(HideezExceptionLocalization.GetErrorAsString(ex));
                await device.Device.RefreshDeviceInfo();
            }
        }

        void UnlockByRfid()
        {
            //try
            //{
            //    await _connectionFlowProcessor.UnlockByRfid(RfidAddress);
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //}
        }

        async Task UpdateFw(DeviceViewModel device)
        {
            // Todo: UpdateFw method must be updated due to new FirmwareImageUploader implementation
            MessageBox.Show("Firmware upload method must be updated due to new FirmwareImageUploader implementation");

            /*
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "firmware image (*.img)|*.img"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    var lo = new LongOperation(1);
                    lo.StateChanged += (sender, e) =>
                    {
                        if (sender is LongOperation longOperation)
                        {
                            device.UpdateFwProgress = longOperation.Progress;
                        }
                    };
                    //var fu = new FirmwareImageUploader(@"d:\fw\HK3_fw_v3.0.2.img", _log);
                    var fu = new FirmwareImageUploader(openFileDialog.FileName, _log);

                    await fu.RunAsync(false, _deviceManager, device.Device, lo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                device.UpdateFwProgress = 0;
            }
            */
        }

        async Task CheckPassphrase(DeviceViewModel device)
        {
            try
            {
                var masterkey = MasterPasswordConverter.GetMasterKey(Passphrase, device.SerialNo);
                await device.Device.CheckPassphrase(masterkey);
                //await device.Device.CheckPassphrase(Encoding.UTF8.GetBytes(Passphrase));
                MessageBox.Show("Passphrase check - ok");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task LinkDevice(DeviceViewModel device)
        {
            try
            {
                
                byte[] code = Encoding.UTF8.GetBytes(CODE);
                byte[] key = MasterPasswordConverter.GetMasterKey(Passphrase, device.SerialNo);
                byte unlockAttempts = 5;// Options 3-15
                await device.Device.Link(key, code, unlockAttempts);
                await device.Device.RefreshDeviceInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task AccessDevice(DeviceViewModel device)
        {
            try
            {
                var wnd = new AccessParamsWindow(AccessParams);
                var res = wnd.ShowDialog();
                if (res == true)
                {
                    var masterkey = MasterPasswordConverter.GetMasterKey(Passphrase, device.SerialNo);
                    await device.Device.Access(
                          DateTime.UtcNow,
                          masterkey,
                          AccessParams);

                    //await device.Device.Access(
                    //    DateTime.UtcNow,
                    //    Encoding.UTF8.GetBytes("passphrase"),
                    //    AccessParams);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task WipeDevice(DeviceViewModel device)
        {
            try
            {
                await device.Device.Wipe(MasterPasswordConverter.GetMasterKey(Passphrase, device.SerialNo));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task WipeDeviceManual(DeviceViewModel device)
        {
            try
            {
                await device.Device.Wipe(Encoding.UTF8.GetBytes(""));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task UnlockDevice(DeviceViewModel device)
        {
            try
            {
                await device.Device.Unlock(MasterPasswordConverter.GetMasterKey(Passphrase, device.SerialNo));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task SetPin(DeviceViewModel device)
        {
            try
            {
                if (await device.Device.SetPin(Pin, OldPin ?? "") != HideezErrorCode.Ok)
                    MessageBox.Show("Wrong old PIN");
                else
                    MessageBox.Show("PIN has been changed");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task ForceSetPin(DeviceViewModel device)
        {
            try
            {
                if (await device.Device.ForceSetPin(Pin, MasterPasswordConverter.GetMasterKey(Passphrase, device.SerialNo)) != HideezErrorCode.Ok)
                    MessageBox.Show("Wrong MasterKey");
                else
                    MessageBox.Show("PIN has been changed");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task EnterPin(DeviceViewModel device)
        {
            try
            {
                if (await device.Device.EnterPin(Pin) != HideezErrorCode.Ok)
                {
                    MessageBox.Show(device.Device.AccessLevel.IsLocked ? "DeviceLocked" : "Wrong PIN");
                }
                else
                {
                    MessageBox.Show("PIN OK");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task DeviceInfo(DeviceViewModel device)
        {
            try
            {
                await device.Device.RefreshDeviceInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task OnConfirmAsync(DeviceViewModel device)
        {
            try
            {
                await device.Device.Confirm(15);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task OnGetOtpAsync(DeviceViewModel device)
        {
            try
            {
                // DPMY UOUO QDCA ABSI AE5D FBAN ESXG OHDV
                var outSecret = await device.Device.ReadStorageAsString((byte)StorageTable.OtpSecrets, 1);
                var otpReply = await device.Device.GetOtp((byte)StorageTable.OtpSecrets, 1);
                MessageBox.Show(otpReply.Otp);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void OpenStorageVindow(DeviceViewModel device)
        {
            try
            {
                var wnd = new StorageWindow(device, _log);
                var res = wnd.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // Load license into specified slot
        async Task LoadLicense(DeviceViewModel device, int slot, string license)
        {
            try
            {
                var byteLicense = Convert.FromBase64String(license);
                await device.Device.LoadLicense(slot, byteLicense);
                MessageBox.Show($"Load license into slot {slot} finished");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // Load license into first free slot
        async Task LoadLicense(DeviceViewModel device, string license)
        {
            try
            {
                var byteLicense = Convert.FromBase64String(license);
                await device.Device.LoadLicense(byteLicense);
                MessageBox.Show($"Load license into free slot finished");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task QueryLicense(DeviceViewModel device, int slot)
        {
            try
            {
                var license = await device.Device.QueryLicense(slot);

                if (license.IsEmpty)
                {
                    MessageBox.Show($"License in slot {slot} is empty");
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"License in slot: {slot}");
                    //sb.AppendLine($"Magic: {license.Magic}");
                    sb.AppendLine($"Issuer: {license.Issuer}");
                    sb.AppendLine($"Features: {ConvertUtils.ByteArrayToString(license.Features)}");
                    sb.AppendLine($"Expires: {license.Expires}");
                    sb.AppendLine($"Text: {license.Text}");
                    sb.AppendLine($"SerialNum: {license.SerialNum}");
                    sb.AppendLine($"Signature: {ConvertUtils.ByteArrayToString(license.Signature)}");

                    MessageBox.Show(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task QueryAllLicenses(DeviceViewModel device)
        {
            try
            {
                var sb = new StringBuilder();
                for (int i = 0; i < 8; i++)
                {
                    var license = await device.Device.QueryLicense(i);

                    if (license.IsEmpty)
                    {
                        sb.AppendLine($"License in slot {i} is empty");
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine($"License in slot: {i}");
                        //sb.AppendLine($"Magic: {license.Magic}");
                        sb.AppendLine($"Issuer: {license.Issuer}");
                        sb.AppendLine($"Features: {ConvertUtils.ByteArrayToString(license.Features)}");
                        sb.AppendLine($"Expires: {license.Expires}");
                        sb.AppendLine($"Text: {license.Text}");
                        sb.AppendLine($"SerialNum: {license.SerialNum}");
                        sb.AppendLine($"Signature: {ConvertUtils.ByteArrayToString(license.Signature)}");
                        sb.AppendLine();
                    }
                }

                MessageBox.Show(sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task QueryActiveLicense(DeviceViewModel device)
        {
            try
            {
                var activeLicense = await device.Device.QueryActiveLicense();

                if (activeLicense.IsEmpty)
                    throw new HideezException(HideezErrorCode.ERR_NO_LICENSE);

                if (activeLicense.Expires < DateTime.UtcNow)
                    throw new HideezException(HideezErrorCode.ERR_LICENSE_EXPIRED);

                var sb = new StringBuilder();
                sb.AppendLine($"Active License");
                //sb.AppendLine($"Magic: {license.Magic}");
                sb.AppendLine($"Issuer: {activeLicense.Issuer}");
                sb.AppendLine($"Features: {ConvertUtils.ByteArrayToString(activeLicense.Features)}");
                sb.AppendLine($"Expires: {activeLicense.Expires}");
                sb.AppendLine($"Text: {activeLicense.Text}");
                sb.AppendLine($"SerialNum: {activeLicense.SerialNum}");
                sb.AppendLine($"Signature: {ConvertUtils.ByteArrayToString(activeLicense.Signature)}");

                MessageBox.Show(sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void CancelConnectionFlow(DeviceViewModel currentDevice)
        {
            _connectionFlowProcessor.Cancel("reason");
        }

        async void SyncDevices()
        {
            try
            {
                await new DeviceStorageReplicator(Devices[0].Device, Devices[1].Device, _log)
                    .Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /*
        async Task DeviceFetchLog(DeviceViewModel device)
        {
            try
            {
                //await device.Device.
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        */

        async Task RunDeviceBackupProcedure(DeviceViewModel currentDevice)
        {
            try
            {
                var cts = new CancellationTokenSource();
                SaveFileDialog dlg = new SaveFileDialog
                {
                    DefaultExt = ".hvb",
                    Filter = "Hideez vault backups (.hvb)|*.hvb", // Filter files by extension
                    FileName = "CredentialsBackup"
                };

                // Show open file dialog box
                var result = dlg.ShowDialog();

                if (result == true)
                {
                    string filename = dlg.FileName;

                    var backupProc = new CredentialsBackupProcedure();

                    await backupProc.Run(currentDevice.Device, filename, Encoding.UTF8.GetBytes(backupPasswordText), cts.Token);
                }

                MessageBox.Show("Backup finished");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async Task RunDeviceRestoreProcedure(DeviceViewModel currentDevice)
        {
            try
            {
                OpenFileDialog dlg = new OpenFileDialog
                {
                    DefaultExt = ".hvb",
                    Filter = "Hideez vault backup (.hvb)|*.hvb|Hideez key backup (*.hb)|*.hb|All files (*.*)|*.*" // Filter files by extension
                };

                // Show open file dialog box
                var result = dlg.ShowDialog();

                if (result == true)
                {
                    string filename = dlg.FileName;

                    var restoreProc = new CredentialsRestoreProcedure();

                    await restoreProc.Run(currentDevice.Device, filename, Encoding.UTF8.GetBytes(backupPasswordText));
                }

                MessageBox.Show("Restore finished");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #region IClientUiProxy
        public event EventHandler<EventArgs> ClientConnected;
        public event EventHandler<PinReceivedEventArgs> PinReceived;
        public event EventHandler<ActivationCodeEventArgs> ActivationCodeReceived;
        public event EventHandler<ActivationCodeEventArgs> ActivationCodeCancelled;
        public event EventHandler<PasswordEventArgs> PasswordReceived;

        public event EventHandler<EventArgs> Connected { add { } remove { } }

        public event EventHandler<EventArgs> PinCancelled { add { } remove { } }

        public Task ShowPinUi(string deviceId, bool withConfirm = false, bool askOldPin = false)
        {
            if (_getPinWindow != null)
                return Task.CompletedTask;

            SendError("", null);

            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_getPinWindow == null)
                    {
                        _getPinWindow = new GetPinWindow((pin, oldPin) =>
                        {
                            PinReceived?.Invoke(this, new PinReceivedEventArgs()
                            {
                                DeviceId = deviceId,
                                OldPin = oldPin,
                                Pin = pin
                            });
                        });

                        _getPinWindow.Show();
                    }
                });
            });
            return Task.CompletedTask;
        }

        public Task HidePinUi()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _getPinWindow?.Hide();
                _getPinWindow = null;
            });

            SendNotification("", null);
            //SendError("");
            return Task.CompletedTask;
        }

        public Task SendStatus(HesStatus hesStatus, RfidStatus rfidStatus, BluetoothStatus dongleStatus, BluetoothStatus bluetoothStatus, HesStatus tbHesStatus)
        {
            ClientUiStatus = $"{DateTime.Now:T} HES: {hesStatus}, RFID: {rfidStatus}, CSR: {dongleStatus}, BLE: {bluetoothStatus}, TB: {tbHesStatus}";
            return Task.CompletedTask;
        }

        public Task SendError(string message, string notificationId)
        {
            ClientUiError = string.IsNullOrEmpty(message) ? "" : $"{DateTime.Now:T} {message}";
            return Task.CompletedTask;
        }

        public Task SendNotification(string message, string notificationId)
        {
            ClientUiNotification = string.IsNullOrEmpty(message) ? "" : $"{DateTime.Now:T} {message}";
            return Task.CompletedTask;
        }

        public Task ShowButtonConfirmUi(string deviceId)
        {
            return Task.CompletedTask;
        }

        public Task ShowActivationCodeUi(string deviceId)
        {
            return Task.CompletedTask;
        }

        public Task HideActivationCodeUi()
        {
            return Task.CompletedTask;
        }

        public Task ShowPasswordUi(string deviceId)
        {
            return Task.CompletedTask;
        }

        public Task HidePasswordUi()
        {
            return Task.CompletedTask;
        }

        #endregion IClientUiProxy

        #region IWorkstationUnlocker
        public bool IsConnected => true;

        public Task<bool> SendLogonRequest(string login, string password, string previousPassword)
        {
            Debug.WriteLine($"IWorkstationUnlocker.SendLogonRequest: {login}, {password}, {previousPassword}");
            return Task.FromResult(true);
        }
        #endregion IWorkstationUnlocker

    }
}
