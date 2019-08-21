﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Hideez.CsrBLE;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.BLE;
using Hideez.SDK.Communication.Command;
using Hideez.SDK.Communication.FW;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.LongOperations;
using Hideez.SDK.Communication.PasswordManager;
using HideezMiddleware;
using HideezMiddleware.Settings;
using Microsoft.Win32;

namespace WinSampleApp.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        readonly EventLogger _log;
        readonly BleConnectionManager _connectionManager;
        readonly BleDeviceManager _deviceManager;
        readonly CredentialProviderConnection _credentialProviderConnection;
        readonly ConnectionFlowProcessor _connectionFlowProcessor;
        readonly RfidServiceConnection _rfidService;

        HesAppConnection _hesConnection;
        byte _nextChannelNo = 2;

        public AccessParams AccessParams { get; set; }

        public string PrimaryAccountLogin { get; set; }
        public string PrimaryAccountPassword { get; set; }

        public string Pin { get; set; }
        public string OldPin { get; set; }

        public string BleAdapterState => _connectionManager?.State.ToString();
        public string ConectByMacAddress
        {
            get { return Properties.Settings.Default.DefaultMac; }
            set
            {
                Properties.Settings.Default.DefaultMac = value;
                Properties.Settings.Default.Save();
            }
        }

        public string RfidAdapterState => "NA";
        public string RfidAddress { get; set; }

        public string HesAddress { get; set; }
        public string HesState => _hesConnection?.State.ToString();

        #region Properties
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
        #endregion Properties


        #region Commands

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
                    CommandAction = (x) =>
                    {
                        SetPin(CurrentDevice);
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
                    CommandAction = (x) =>
                    {
                        ForceSetPin(CurrentDevice);
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
                        EnterPin(CurrentDevice);
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
                        LinkDevice(CurrentDevice);
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
                        AccessDevice(CurrentDevice);
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
                        WipeDevice(CurrentDevice);
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
                        UnlockDevice(CurrentDevice);
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
                        return _connectionManager.State == BluetoothAdapterState.PoweredOn && !BleAdapterDiscovering;
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
                        return _connectionManager.State == BluetoothAdapterState.PoweredOn && BleAdapterDiscovering;
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
                        RemoveAllDevices(x);
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
        public ICommand ConnectBondedDeviceCommand
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
                        ConnectDeviceByMac(ConectByMacAddress);
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
                        DisconnectDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand ReadDeviceCommand
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
                        ReadDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand WriteDeviceCommand
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
                        WriteDevice(CurrentDevice);
                    }
                };
            }
        }

        public ICommand LoadDeviceCommand
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
                        LoadDevice(CurrentDevice);
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
                        PingDevice(CurrentDevice);
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

        public ICommand BoostDeviceRssiCommand
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
                        BoostDeviceRssi(CurrentDevice);
                    }
                };
            }
        }

        public ICommand UpdateFwCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = () =>
                    {
                        return CurrentDevice != null && (CurrentDevice.UpdateFwProgress == 0 || CurrentDevice.UpdateFwProgress == 100);
                    },
                    CommandAction = (x) =>
                    {
                        UpdateFw(CurrentDevice);
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
                        WritePrimaryAccount(CurrentDevice);
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
                        DeviceInfo(CurrentDevice);
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
                        OnConfirmAsync(CurrentDevice);
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
                        OnGetOtpAsync(CurrentDevice);
                    }
                };
            }
        }
        #endregion

        public MainWindowViewModel()
        {
            AccessParams = new AccessParams()
            {
                MasterKey_Bond = true,
                MasterKey_Connect = false,
                MasterKey_Link = false,
                MasterKey_Channel = false,

                Button_Bond = false,
                Button_Connect = false,
                Button_Link = true,
                Button_Channel = true,

                Pin_Bond = false,
                Pin_Connect = true,
                Pin_Link = false,
                Pin_Channel = false,

                PinMinLength = 4,
                PinMaxTries = 3,
                MasterKeyExpirationPeriod = 24 * 60 * 60,
                PinExpirationPeriod = 15 * 60,
                ButtonExpirationPeriod = 15,
            };

            HesAddress = "https://localhost:44371";

            _log = new EventLogger("ExampleApp");
            _connectionManager = new BleConnectionManager(_log, "d:\\temp\\bonds"); //todo

            _connectionManager.AdapterStateChanged += ConnectionManager_AdapterStateChanged;
            _connectionManager.DiscoveryStopped += ConnectionManager_DiscoveryStopped;
            _connectionManager.DiscoveredDeviceAdded += ConnectionManager_DiscoveredDeviceAdded;
            _connectionManager.DiscoveredDeviceRemoved += ConnectionManager_DiscoveredDeviceRemoved;
            _connectionManager.AdvertismentReceived += ConnectionManager_AdvertismentReceived;

            // BLE ============================
            _deviceManager = new BleDeviceManager(_log, _connectionManager);
            _deviceManager.DeviceAdded += DevicesManager_DeviceCollectionChanged;
            _deviceManager.DeviceRemoved += DevicesManager_DeviceCollectionChanged;


            // Named Pipes Server ==============================
            _credentialProviderConnection = new CredentialProviderConnection(_log);
            _credentialProviderConnection.Start();


            // RFID Service Connection ============================
            _rfidService = new RfidServiceConnection(_log);
            _rfidService.Start();

            // Unlocker Settings Manager ==================================
            var unlockerSettingsManager = new SettingsManager<UnlockerSettings>(string.Empty, new XmlFileSerializer(_log));

            // UI proxy ==================================
            var uiProxy = new UiProxy(_credentialProviderConnection);

            // ConnectionFlowProcessor ==================================
            _connectionFlowProcessor = new ConnectionFlowProcessor(
                _deviceManager, 
                _hesConnection,
                _rfidService, 
                _connectionManager,
                _credentialProviderConnection, // as IWorkstationUnlocker
                null, 
                unlockerSettingsManager, 
                uiProxy);

            // StatusManager =============================
            var statusManager = new StatusManager(_hesConnection, _rfidService, _connectionManager, _credentialProviderConnection, uiProxy);

            _connectionManager.StartDiscovery();
        }

        async void ConnectHes()
        {
            try
            {
                // HES
                //_hesConnection = new HesAppConnection(_deviceManager, "http://192.168.10.241", _log);
                //_hesConnection = new HesAppConnection(_deviceManager, "https://localhost:44371", _log);
                //_hesConnection = new HesAppConnection(_deviceManager, "http://192.168.10.249", _log);

                await DisconnectHes();

                if (!string.IsNullOrEmpty(HesAddress))
                {
                    _hesConnection = new HesAppConnection(_deviceManager, HesAddress, new WorkstationInfoProvider("", _log), _log);

                    _hesConnection.Start();

                    _hesConnection.HubConnectionStateChanged += HesConnection_HubConnectionStateChanged;

                    //_workstationUnlocker.SetHes(_hesConnection);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                NotifyPropertyChanged(nameof(HesState));
            }
        }

        async Task DisconnectHes()
        {
            try
            {
                if (_hesConnection != null)
                {
                    _hesConnection.HubConnectionStateChanged -= HesConnection_HubConnectionStateChanged;
                    await _hesConnection.Stop();
                    _hesConnection = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                NotifyPropertyChanged(nameof(HesState));
            }
        }

        void HesConnection_HubConnectionStateChanged(object sender, EventArgs e)
        {
            NotifyPropertyChanged(nameof(HesState));
        }

        internal async void Close()
        {
            if (_hesConnection != null)
                await _hesConnection.Stop();
            _rfidService?.Stop();
            _credentialProviderConnection?.Stop();
        }

        void DevicesManager_DeviceCollectionChanged(object sender, DeviceCollectionChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.AddedDevice != null)
                {
                    var deviceViewModel = new DeviceViewModel(e.AddedDevice);
                    Devices.Add(deviceViewModel);
                    if (CurrentDevice == null)
                        CurrentDevice = deviceViewModel;
                }
                else if (e.RemovedDevice != null)
                {
                    var item = Devices.FirstOrDefault(x => x.Id == e.RemovedDevice.Id &&
                                                           x.ChannelNo == e.RemovedDevice.ChannelNo);

                    if (item != null)
                        Devices.Remove(item);
                }
            });
        }

        void ConnectionManager_AdvertismentReceived(object sender, AdvertismentReceivedEventArgs e)
        {
            //_log.WriteLine("MainVM", $"{e.Id} - {e.Rssi}");
            //if (e.Rssi > -25)
            //{
            //    _log.WriteLine("MainVM", $"-------------- {e.Id} - {e.Rssi}");
            //    ConnectDeviceByMac(e.Id);
            //}
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
            Application.Current.Dispatcher.Invoke(() =>
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
            _connectionManager.Restart();
        }

        void StartDiscovery()
        {
            //DiscoveredDevices.Clear();
            _connectionManager.StartDiscovery();
            BleAdapterDiscovering = true;
        }

        void StopDiscovery()
        {
            _connectionManager.StopDiscovery();
            BleAdapterDiscovering = false;
            DiscoveredDevices.Clear();
        }

        void ClearDiscoveredDeviceList()
        {
            DiscoveredDevices.Clear();
        }

        async void RemoveAllDevices(object x)
        {
            try
            {
                await _deviceManager.RemoveAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void ConnectDiscoveredDevice(DiscoveredDeviceAddedEventArgs e)
        {
            _connectionManager.ConnectDiscoveredDeviceAsync(e.Id);
        }

        async void ConnectDeviceByMac(string mac)
        {
            try
            {
                _log.WriteLine("MainVM", $"Waiting Device connectin {mac} ..........................");

                var device = await _deviceManager.ConnectByMac(mac, timeout: 10_000);

                if (device != null)
                    _log.WriteLine("MainVM", $"Device connected {device.Name} ++++++++++++++++++++++++");
                else
                    _log.WriteLine("MainVM", $"Device NOT connected --------------------------");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void ConnectDevice(DeviceViewModel device)
        {
            try
            {
                device.Device.Connection.Connect();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void DisconnectDevice(DeviceViewModel device)
        {
            device.Device.Disconnect();
        }

        async void PingDevice(DeviceViewModel device)
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
                MessageBox.Show(ex.Message);
            }
        }

        async void ReadDevice(DeviceViewModel device)
        {
            try
            {
                var readResult = await device.Device.ReadStorageAsString(35, 1);

                if (readResult == null)
                    MessageBox.Show("Empty");
                else
                    MessageBox.Show(readResult);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void WriteDevice(DeviceViewModel device)
        {
            try
            {
                var pm = new DevicePasswordManager(device.Device, _log);

                // array of records
                //for (int i = 0; i < 100; i++)
                //{
                //    var account = new AccountRecord()
                //    {
                //        Key = 0,
                //        Name = $"My Google Account {i}",
                //        Login = $"admin_{i}@hideez.com",
                //        Password = $"my_password_{i}",
                //        OtpSecret = $"asdasd_{i}",
                //        Apps = $"12431412412342134_{i}",
                //        Urls = $"www.hideez.com;www.google.com_{i}",
                //        IsPrimary = i == 0
                //    };

                //    var key = await pm.SaveOrUpdateAccount(account.Key, account.Flags, account.Name,
                //        account.Password, account.Login, account.OtpSecret,
                //        account.Apps, account.Urls,
                //        account.IsPrimary);

                //    Debug.WriteLine($"^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^ Writing {i} account");
                //}


                // single record
                var account = new AccountRecord()
                {
                    Key = 1,
                    Name = $"My Google Account 0",
                    Login = $"admin_0@hideez.com",
                    Password = $"my_password_0",
                    OtpSecret = $"DPMYUOUOQDCAABSIAE5DFBANESXGOHDV",
                    Apps = $"12431412412342134_0",
                    Urls = $"www.hideez.com;www.google.com_0",
                    IsPrimary = true
                };

                var key = await pm.SaveOrUpdateAccount(account.Key, account.Name,
                    account.Password, account.Login, account.OtpSecret,
                    account.Apps, account.Urls,
                    account.IsPrimary
                    //,(ushort)(StorageTableFlags.RESERVED7 | StorageTableFlags.RESERVED6) 
                    //,(ushort)(StorageTableFlags.RESERVED7 | StorageTableFlags.RESERVED6)
                    );
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void WritePrimaryAccount(DeviceViewModel device)
        {
            try
            {
                var pm = new DevicePasswordManager(device.Device, _log);

                var account = new AccountRecord()
                {
                    Key = 1,
                    Name = $"My Primary Account",
                    Login = PrimaryAccountLogin,
                    Password = PrimaryAccountPassword,
                    OtpSecret = null,
                    Apps = null,
                    Urls = null,
                    IsPrimary = true
                };

                var key = await pm.SaveOrUpdateAccount(account.Key, account.Name,
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

        async void LoadDevice(DeviceViewModel device)
        {
            try
            {
                var pm = new DevicePasswordManager(device.Device, _log);
                await pm.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void AddDeviceChannel(DeviceViewModel currentDevice)
        {
            IDevice newDevice = await _deviceManager.AddChannel(currentDevice.Device, _nextChannelNo++, isRemote: false);
        }

        async void RemoveDeviceChannel(DeviceViewModel currentDevice)
        {
            await _deviceManager.Remove(currentDevice.Device);
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
                                //var pingText = $"{device.Id} {DateTime.Now} qwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqerqwerqwerqwer qwer qwe rwer wqe rqqqqqqqqqqqqqqqqqqqqqqqqqqwer qwer qwer qwerwqr wqer";
                                //pingText = pingText + pingText + pingText;
                                var pingText = $"{device.Id}_{i}";
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

        async void BoostDeviceRssi(DeviceViewModel device)
        {
            try
            {
                await device.Device.BoostRssi(100, 15);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void UnlockByRfid()
        {
            try
            {
                await _connectionFlowProcessor.UnlockByRfid(RfidAddress);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void UpdateFw(DeviceViewModel device)
        {
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

                    await fu.RunAsync(false, device.Device, lo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                device.UpdateFwProgress = 0;
            }
        }

        async void LinkDevice(DeviceViewModel device)
        {
            try
            {
                await device.Device.Link(Encoding.UTF8.GetBytes("passphrase"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void AccessDevice(DeviceViewModel device)
        {
            try
            {
                var wnd = new AccessParamsWindow(AccessParams);
                var res = wnd.ShowDialog();
                if (res == true)
                {
                    await device.Device.Access(
                        DateTime.UtcNow,
                        Encoding.UTF8.GetBytes("passphrase"),
                        AccessParams);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void WipeDevice(DeviceViewModel device)
        {
            try
            {
                await device.Device.Wipe(Encoding.UTF8.GetBytes("passphrase"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void UnlockDevice(DeviceViewModel device)
        {
            try
            {
                await device.Device.Unlock(Encoding.UTF8.GetBytes("passphrase"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void SetPin(DeviceViewModel device)
        {
            try
            {
                if (!await device.Device.SetPin(Pin, OldPin ?? ""))
                    MessageBox.Show("Wrong old PIN");
                else
                    MessageBox.Show("PIN has been changed");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void ForceSetPin(DeviceViewModel device)
        {
            try
            {
                if (!await device.Device.ForceSetPin(Pin, Encoding.UTF8.GetBytes("passphrase")))
                    MessageBox.Show("Wrong MasterKey");
                else
                    MessageBox.Show("PIN has been changed");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void EnterPin(DeviceViewModel device)
        {
            try
            {
                if (!await device.Device.EnterPin(Pin))
                   MessageBox.Show("Wrong PIN");
                else
                    MessageBox.Show("PIN OK");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        async void DeviceInfo(DeviceViewModel device)
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

        async void OnConfirmAsync(DeviceViewModel device)
        {
            try
            {
                var reply = await device.Device.Confirm(5);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void OnGetOtpAsync(DeviceViewModel device)
        {
            try
            {
                // DPMY UOUO QDCA ABSI AE5D FBAN ESXG OHDV
                var outSecret = await device.Device.ReadStorageAsString((byte)StorageTable.OtpSecrets, 1);
                var otpReply = await device.Device.GetOtp((byte)StorageTable.OtpSecrets, 1);
                MessageBox.Show(otpReply.Otp);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
