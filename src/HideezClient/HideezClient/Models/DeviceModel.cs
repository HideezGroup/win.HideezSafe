using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Device;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.PasswordManager;
using Hideez.SDK.Communication.Utils;
using HideezClient.Messages;
using HideezClient.Modules;
using HideezClient.Mvvm;
using HideezClient.Utilities;
using HideezMiddleware;
using Microsoft.Win32;
using MvvmExtensions.Attributes;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hideez.SDK.Communication.Log;
using HideezClient.Modules.Log;
using HideezMiddleware.IPC.DTO;
using Meta.Lib.Modules.PubSub;
using HideezMiddleware.IPC.IncommingMessages;
using HideezMiddleware.Localize;
using Hideez.SDK.Communication.HES.DTO;
using HideezMiddleware.Utils.WorkstationHelper;
using HideezMiddleware.ApplicationModeProvider;
using HideezMiddleware.Utils;
using HideezClient.Messages.Dialogs.Pin;
using HideezClient.Messages.Dialogs.MasterPassword;
using HideezClient.Tasks;
using HideezClient.Messages.Dialogs.Wipe;
using HideezClient.Messages.Dialogs;
using HideezClient.Dialogs;

namespace HideezClient.Models
{
    // Todo: Implement thread-safety lock for password manager and remote device
    public class DeviceModel : ObservableObject, IDisposable
    {
        const int INIT_TIMEOUT = 5_000;
        readonly int CREDENTIAL_TIMEOUT = SdkConfig.MainWorkflowTimeout;

        readonly Logger _log = LogManager.GetCurrentClassLogger(nameof(DeviceModel));
        readonly IRemoteDeviceFactory _remoteDeviceFactory;
        readonly IMetaPubSub _metaMessenger;
        readonly IMetaPubSub _remoteDeviceMessenger = new MetaPubSub(new MetaPubSubLogger(new NLogWrapper()));

        ApplicationMode _applicationMode;
        Device _remoteDevice;
        DelayedMethodCaller dmc = new DelayedMethodCaller(2500);
        bool storageSyncEnabled = true;

        string id;
        string connectionId;
        string notificationsId;
        string name;
        string ownerName;
        string ownerEmail;
        bool isConnected;
        bool canRemoveConnection;
        bool canDisconnect;
        bool isInitialized;
        string serialNo;
        string mac;
        uint storageTotalSize;
        uint storageFreeSize;
        Version firmwareVersion;
        Version bootloaderVersion;
        int pinAttemptsRemain;
        bool isCanUnlock;
        int minPinLength;
        int unlockAttemptsRemain;
        double proximity = 0;
        int battery = 0;
        bool finishedMainFlow;
        byte storageUpdateCounter;
        DateTime _snapshotTime = DateTime.MinValue;

        bool isCreatingRemoteDevice;
        bool isAuthorizingRemoteDevice;
        bool isLoadingStorage;
        bool isStorageLoaded;
        bool isProximityLockEnabled;

        event EventHandler RemoteDeviceShutdown;

        int _interlockedRemote = 0;

        bool _isStorageLocked = false;

        public DeviceModel(
            IRemoteDeviceFactory remoteDeviceFactory,
            IMetaPubSub metaMessenger,
            DeviceDTO dto,
            ApplicationMode applicationMode)
        {
            _remoteDeviceFactory = remoteDeviceFactory;
            _metaMessenger = metaMessenger;
            _applicationMode = applicationMode;

            PropertyChanged += Device_PropertyChanged;

            _metaMessenger.TrySubscribeOnServer<HideezMiddleware.IPC.Messages.DeviceConnectionStateChangedMessage>(OnDeviceConnectionStateChanged);
            _metaMessenger.TrySubscribeOnServer<HideezMiddleware.IPC.Messages.DeviceInitializedMessage>(OnDeviceInitialized);
            _metaMessenger.TrySubscribeOnServer<HideezMiddleware.IPC.Messages.DeviceFinishedMainFlowMessage>(OnDeviceFinishedMainFlow);
            _metaMessenger.TrySubscribeOnServer<HideezMiddleware.IPC.Messages.DeviceProximityChangedMessage>(OnDeviceProximityChanged);
            _metaMessenger.TrySubscribeOnServer<HideezMiddleware.IPC.Messages.DeviceBatteryChangedMessage>(OnDeviceBatteryChanged);
            _metaMessenger.TrySubscribeOnServer<HideezMiddleware.IPC.Messages.DeviceProximityLockEnabledMessage>(OnDeviceProximityLockEnabled);
            _metaMessenger.TrySubscribeOnServer<HideezMiddleware.IPC.Messages.LockDeviceStorageMessage>(OnLockDeviceStorage);
            _metaMessenger.TrySubscribeOnServer<HideezMiddleware.IPC.Messages.LiftDeviceStorageLockMessage>(OnLiftDeviceStorageLock);
            _metaMessenger.Subscribe<SessionSwitchMessage>(OnSessionSwitch);
            _metaMessenger.Subscribe<EnablePasswordManagerSyncOnChange>(OnEnablePasswordManagerSyncOnChange, msg => msg.DeviceId == Id);
            _metaMessenger.Subscribe<DisablePasswordManagerSyncOnChange>(OnDisablePasswordManagerSyncOnChange, msg => msg.DeviceId == Id);
            _metaMessenger.Subscribe<ForcePasswordManagerSync>(OnForcePasswordManagerSync, msg => msg.DeviceId == Id);

            RegisterDependencies();

            LoadFrom(dto);

            CanRemoveConnection = dto.ConnectionType == (byte)DefaultConnectionIdProvider.Csr;
            CanDisconnect = dto.ConnectionType == (byte)DefaultConnectionIdProvider.Csr;
        }

        #region Properties
        public IDeviceStorage Storage
        {
            get
            {
                return _remoteDevice;
            }
        }

        public DevicePasswordManager PasswordManager { get; private set; }

        public string TypeName { get; } = "Hardware Vault";

        // There properties are set by constructor and updated by certain events and messages
        
        // Virtual device identifier
        public string Id
        {
            get { return id; }
            private set { Set(ref id, value); }
        }

        // This property is used as id for notifications related to this device
        public string NotificationsId
        {
            get { return notificationsId; }
            private set { Set(ref notificationsId, value); }
        }

        public string Name
        {
            get { return name; }
            private set { Set(ref name, value); }
        }

        public string OwnerName
        {
            get { return ownerName; }
            private set { Set(ref ownerName, value); }
        }

        public string OwnerEmail
        {
            get { return ownerEmail; }
            private set { Set(ref ownerEmail, value); }
        }

        public bool IsConnected
        {
            get { return isConnected; }
            private set { Set(ref isConnected, value); }
        }

        public bool CanRemoveConnection
        {
            get { return canRemoveConnection; }
            private set { Set(ref canRemoveConnection, value); }
        }

        public bool CanDisconnect
        {
            get { return canDisconnect; }
            private set { Set(ref canDisconnect, value); }
        }

        // TODO: This property is no longer required
        public bool IsInitialized
        {
            get { return isInitialized; }
            set { Set(ref isInitialized, value); }
        }

        public string SerialNo
        {
            get { return serialNo; }
            private set { Set(ref serialNo, value); }
        }

        public string Mac
        {
            get { return mac; }
            private set { Set(ref mac, value); }
        }

        public Version FirmwareVersion
        {
            get { return firmwareVersion; }
            private set { Set(ref firmwareVersion, value); }
        }

        public Version BootloaderVersion
        {
            get { return bootloaderVersion; }
            private set { Set(ref bootloaderVersion, value); }
        }

        public uint StorageTotalSize
        {
            get { return storageTotalSize; }
            private set { Set(ref storageTotalSize, value); }
        }

        public uint StorageFreeSize
        {
            get { return storageFreeSize; }
            private set { Set(ref storageFreeSize, value); }
        }

        [DependsOn(nameof(IsConnected), nameof(IsInitialized), nameof(FinishedMainFlow))]
        public bool IsInitializing
        {
            get { return IsConnected && (!IsInitialized || !FinishedMainFlow); }
        }

        [DependsOn(nameof(IsConnected))]
        public double Proximity
        {
            get { return IsConnected ? proximity : 0; }
            set { Set(ref proximity, value); }
        }

        [DependsOn(nameof(IsConnected))]
        public int Battery
        {
            get { return IsConnected ? battery : 0; }
            set { Set(ref battery, value); }
        }

        public bool FinishedMainFlow
        {
            get { return finishedMainFlow; }
            set { Set(ref finishedMainFlow, value); }
        }

        // These properties depend upon some internal processes 
        public bool IsCreatingRemoteDevice
        {
            get { return isCreatingRemoteDevice; }
            set { Set(ref isCreatingRemoteDevice, value); }
        }
        
        public bool IsAuthorizingRemoteDevice
        {
            get { return isAuthorizingRemoteDevice; }
            private set { Set(ref isAuthorizingRemoteDevice, value); }
        }

        public bool IsLoadingStorage
        {
            get { return isLoadingStorage; }
            private set { Set(ref isLoadingStorage, value); }
        }

        public bool IsStorageLoaded
        {
            get { return isStorageLoaded; }
            private set { Set(ref isStorageLoaded, value); }
        }


        // These properties are tied to the RemoteDevice 
        [DependsOn(nameof(IsConnected))]
        public AccessLevel AccessLevel
        {
            get { return _remoteDevice?.AccessLevel; }
        }

        public int PinAttemptsRemain
        {
            get { return _remoteDevice != null ? (int)_remoteDevice?.PinAttemptsRemain : 0; }
        }

        [DependsOn(nameof(AccessLevel))]
        public bool IsAuthorized
        {
            get
            {
                if (_remoteDevice == null)
                    return false;

                if (_remoteDevice.AccessLevel == null)
                    return false;

                return _remoteDevice.AccessLevel.IsAllOk;
            }
        }

        public bool CanLockByProximity
        {
            get { return isProximityLockEnabled; }
            private set { Set(ref isProximityLockEnabled, value); }
        }

        public int OtherConnections
        {
            get { return _remoteDevice != null ? (int)_remoteDevice?.OtherConnections : 0; }
        }

        public bool IsCanUnlock
        {
            get { return isCanUnlock; }
            private set { Set(ref isCanUnlock, value); }
        }

        public int MinPinLength
        {
            get { return minPinLength; }
            private set { Set(ref minPinLength, value); }
        }

        public int UnlockAttemptsRemain
        {
            get { return unlockAttemptsRemain; }
            private set { Set(ref unlockAttemptsRemain, value); }
        }

        public bool IsStorageLocked
        {
            get { return _isStorageLocked; }
            private set { Set(ref _isStorageLocked, value); }
        }
        #endregion

        #region Messege & Event handlers
        void RemoteDevice_ButtonPressed(object sender, ButtonPressCode e)
        {
            _log.WriteLine($"({SerialNo}) Vault button pressed, code: {e}");

            Task.Run(() =>
            {
                _metaMessenger.Publish(new DeviceButtonPressedMessage(Id, e));
            });
        }

        void RemoteDevice_StorageModified(object sender, EventArgs e)
        {
            _log.WriteLine($"({SerialNo}) Vault storage modified");

            Task.Run(() =>
            {
                dmc.CallMethod(async () =>
                {
                    if (_remoteDevice != null && PasswordManager != null && storageSyncEnabled)
                    {
                        var updateCounter = _remoteDevice.StorageUpdateCounter;
                        var loadedUpdateCounter = PasswordManager.LoadedStorageUpdateCounter;
                        var delta = loadedUpdateCounter - updateCounter;
                        if (updateCounter > loadedUpdateCounter || delta > 0)
                        {
                            try
                            {
                                await LoadStorage();
                            }
                            catch (Exception ex)
                            {
                                _log.WriteLine(ex);
                            }
                        }
                    }        
                });
            });

        }

        void RemoteDevice_PropertyChanged(object sender, string e)
        {
            switch (e)
            {
                case nameof(AccessLevel):
                case nameof(PinAttemptsRemain):
                    RaisePropertyChanged(e);
                    break;
                default:
                    break;
            }
        }

        async void RemoteDevice_WipeFinished(object sender, WipeFinishedEventtArgs e)
        {
            try
            {
                await _metaMessenger.Publish(new WipeFinishedMessage(Id, e.Status));
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
        }

        Task OnDeviceConnectionStateChanged(HideezMiddleware.IPC.Messages.DeviceConnectionStateChangedMessage obj)
        {
            if (obj.Device.Id == Id)
                LoadFrom(obj.Device);

            return Task.CompletedTask; 
        }

        Task OnDeviceInitialized(HideezMiddleware.IPC.Messages.DeviceInitializedMessage obj)
        {
            if (obj.Device.Id == Id)
                LoadFrom(obj.Device);
            
            return Task.CompletedTask;
        }

        Task OnDeviceFinishedMainFlow(HideezMiddleware.IPC.Messages.DeviceFinishedMainFlowMessage obj)
        {
            if (obj.Device.Id == Id)
                LoadFrom(obj.Device);

            return Task.CompletedTask;
        }

        Task OnSessionSwitch(SessionSwitchMessage obj)
        {
            switch (obj.Reason)
            {
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.SessionUnlock:
                    // Workstation unlock is one of reasons to try create remote device
                    Task.Run(TryInitRemoteAsync);
                    break;
                default:
                    return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        Task OnDeviceProximityChanged(HideezMiddleware.IPC.Messages.DeviceProximityChangedMessage obj)
        {
            if (obj.DeviceId == Id )
                Proximity = obj.Proximity;

            return Task.CompletedTask;
        }

        Task OnDeviceBatteryChanged(HideezMiddleware.IPC.Messages.DeviceBatteryChangedMessage obj)
        {
            if (obj.DeviceId == Id)
                Battery = obj.Battery;

            return Task.CompletedTask;
        }

        Task OnDeviceProximityLockEnabled(HideezMiddleware.IPC.Messages.DeviceProximityLockEnabledMessage obj)
        {
            if (obj.Device.Id == Id)
                LoadFrom(obj.Device);

            return Task.CompletedTask;
        }

        Task OnLockDeviceStorage(HideezMiddleware.IPC.Messages.LockDeviceStorageMessage obj)
        {
            _log.WriteLine($"Lock vault storage ({obj.SerialNo})");

            if (obj.SerialNo == SerialNo)
            {
                IsStorageLocked = true;
                _metaMessenger.Publish(new ShowInfoNotificationMessage(string.Format(TranslationSource.Instance["Vault.Notification.Synchronizing"], SerialNo, Environment.NewLine), notificationId: NotificationsId));
            }

            return Task.CompletedTask;
        }

        Task OnLiftDeviceStorageLock(HideezMiddleware.IPC.Messages.LiftDeviceStorageLockMessage obj)
        {
            _log.WriteLine($"Lift vault storage lock ({obj.SerialNo})");

            if (obj.SerialNo == SerialNo || string.IsNullOrWhiteSpace(obj.SerialNo))
                IsStorageLocked = false;

            return Task.CompletedTask;
        }

        async void Device_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FinishedMainFlow))
            {
                await TryInitRemoteAsync();
            }

            if (e.PropertyName == nameof(IsConnected) && !IsConnected)
            {
                await TryShutdownRemoteAsync();
            }
        }

        Task OnEnablePasswordManagerSyncOnChange(EnablePasswordManagerSyncOnChange arg)
        {
            storageSyncEnabled = true;
            return Task.CompletedTask;
        }

        Task OnDisablePasswordManagerSyncOnChange(DisablePasswordManagerSyncOnChange arg)
        {
            storageSyncEnabled = false;
            return Task.CompletedTask;
        }

        Task OnForcePasswordManagerSync(ForcePasswordManagerSync arg)
        {
            Task.Run(async () =>
            {
                if (_remoteDevice != null && PasswordManager != null)
                {
                    try
                    {
                        await LoadStorage();
                    }
                    catch (Exception ex)
                    {
                        _log.WriteLine(ex);
                    }
                }
            });
            return Task.CompletedTask;
        }

        #endregion

        void LoadFrom(DeviceDTO dto)
        {
            if (dto.SnapshotTime > _snapshotTime)
            {
                _snapshotTime = dto.SnapshotTime;
                Id = dto.Id;
                NotificationsId = dto.NotificationsId;
                Name = dto.Name;
                OwnerName = dto.OwnerName;
                OwnerEmail = dto.OwnerEmail;
                IsConnected = dto.IsConnected;
                IsInitialized = dto.IsInitialized;
                SerialNo = dto.SerialNo;
                Mac = dto.Mac;
                FirmwareVersion = dto.FirmwareVersion;
                BootloaderVersion = dto.BootloaderVersion;
                StorageTotalSize = dto.StorageTotalSize;
                StorageFreeSize = dto.StorageFreeSize;
                Proximity = dto.Proximity;
                Battery = dto.Battery;
                FinishedMainFlow = dto.HwVaultConnectionState == HwVaultConnectionState.Online;
                CanLockByProximity = dto.CanLockPyProximity;
                IsCanUnlock = dto.IsCanUnlock;
                MinPinLength = dto.MinPinLength;
                UnlockAttemptsRemain = dto.UnlockAttemptsRemain;
            }
        }

        async Task TryInitRemoteAsync()
        {
            if (FinishedMainFlow)
            {
                try
                {
                    await InitRemoteAndLoadStorageAsync(false);
                }
                catch (Exception ex)
                {
                    _log.WriteLine(ex);
                }
            }
        }

        async Task TryShutdownRemoteAsync()
        {
            try
            {
                await ShutdownRemoteDeviceAsync(HideezErrorCode.DeviceDisconnected);
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
        }

        /// <summary>
        /// Create and authorize remote device, then load credentials storage from this device
        /// </summary>
        /// <param name="authorizeDevice">If false, skip remote device authorization step. Default is true.</param>
        public async Task InitRemoteAndLoadStorageAsync(bool authorizeDevice = true)
        {
            if (Interlocked.CompareExchange(ref _interlockedRemote, 1, 0) == 0)
            {
                try
                {
                    if (!IsCreatingRemoteDevice && 
                        !IsAuthorizingRemoteDevice && 
                        !IsLoadingStorage && 
                        WorkstationInformationHelper.GetCurrentSessionLockState() == WorkstationInformationHelper.LockState.Unlocked)
                    {
                        using (var authCts = new CancellationTokenSource())
                        {
                            void authCtsCancel(object sender, EventArgs e) { authCts.Cancel(); }
                            try
                            {
                                RemoteDeviceShutdown += authCtsCancel;

                                using (var remoteCts = new CancellationTokenSource())
                                {
                                    void remoteCtsCancel(object sender, EventArgs e) { remoteCts.Cancel(); }
                                    try
                                    {
                                        RemoteDeviceShutdown += remoteCtsCancel;
                                        await CreateRemoteDeviceAsync(remoteCts.Token);

                                        if (remoteCts.IsCancellationRequested)
                                        {
                                            _log.WriteLine($"({SerialNo}) Remote vault creation cancelled");
                                            return;
                                        }
                                    }
                                    finally
                                    {
                                        RemoteDeviceShutdown -= remoteCtsCancel;
                                    }
                                }

                                if (_remoteDevice != null)
                                {
                                    _log.WriteLine($"({_remoteDevice.SerialNo}) Remote vault created");
                                    await _remoteDevice.RefreshDeviceInfo();
                                    if (_remoteDevice.AccessLevel != null)
                                    {
                                        _log.WriteLine($"({_remoteDevice.SerialNo}) access profile (allOk:{_remoteDevice.AccessLevel.IsAllOk}; " +
                                            $"pin:{_remoteDevice.AccessLevel.IsPinRequired}; " +
                                            $"newPin:{_remoteDevice.AccessLevel.IsNewPinRequired}; " +
                                            $"button:{_remoteDevice.AccessLevel.IsButtonRequired}; " +
                                            $"link:{_remoteDevice.AccessLevel.IsLinkRequired}; " +
                                            $"master:{_remoteDevice.AccessLevel.IsMasterKeyRequired}; " +
                                            $"locked:{_remoteDevice.AccessLevel.IsLocked})");
                                    }
                                    else
                                        _log.WriteLine($"({_remoteDevice.SerialNo}) access level is null");
                                }

                            if (_remoteDevice?.IsLockedByCode == true)
                                throw new HideezException(HideezErrorCode.DeviceIsLocked);
                            else if (authorizeDevice && !IsAuthorized && _remoteDevice?.AccessLevel != null && !_remoteDevice.AccessLevel.IsAllOk)
                                await AuthorizeRemoteDevice(authCts.Token);
                            else if (!authorizeDevice && !IsAuthorized && _remoteDevice?.AccessLevel != null && _remoteDevice.AccessLevel.IsAllOk)
                                await AuthorizeRemoteDevice(authCts.Token);
                            else if (_remoteDevice?.AccessLevel != null && _remoteDevice.AccessLevel.IsLocked)
                                throw new HideezException(HideezErrorCode.DeviceIsLocked);

                                if (!authCts.IsCancellationRequested && !IsStorageLoaded)
                                    await LoadStorage();
                            }
                            catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.DeviceDisconnected)
                            {
                                _log.WriteLine("Remote vault creation aborted, vault disconnected", LogErrorSeverity.Warning);
                            }
                            catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.DeviceIsLocked)
                            {
                                if (_remoteDevice.IsLockedByCode)
                                {
                                    await _metaMessenger.Publish(new ShowLockNotificationMessage(TranslationSource.Instance["Notification.DeviceLockedByCode.Message"],
                                        TranslationSource.Instance["Notification.DeviceLockedByCode.Caption"],
                                        new NotificationOptions() { CloseTimeout = NotificationOptions.LongTimeout },
                                        NotificationsId));
                                }
                                else if (_remoteDevice.IsLockedByPin)
                                {
                                    await _metaMessenger.Publish(new ShowLockNotificationMessage(TranslationSource.Instance["Notification.DeviceLockedByPin.Message"],
                                        TranslationSource.Instance["Notification.DeviceLockedByPin.Caption"],
                                        new NotificationOptions() { CloseTimeout = NotificationOptions.LongTimeout },
                                        NotificationsId));
                                }
                                else
                                {
                                    _log.WriteLine(ex);
                                }
                            }
                            catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.DeviceIsLockedByPin)
                            {
                            }
                            catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.DeviceIsLockedByCode)
                            {
                            }
                            catch (HideezException ex)
                            {
                                _log.WriteLine("Encryption desync, discarding remote device");
                                await ShutdownRemoteDeviceAsync(ex.ErrorCode);
                            }
                            catch (Exception ex)
                            {
                                ShowError(ex.Message);
                            }
                            finally
                            {
                                RemoteDeviceShutdown -= authCtsCancel;
                            }
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _interlockedRemote, 0);
                }
            }
        }

        public async Task ShutdownRemoteDeviceAsync(HideezErrorCode code)
        {
            try
            {
                if (_remoteDevice != null)
                {
                    RemoteDeviceShutdown?.Invoke(this, EventArgs.Empty);

                    var tempRemoteDevice = _remoteDevice;
                    _remoteDevice = null;
                    PasswordManager = null;

                    if (tempRemoteDevice != null)
                    {
                        tempRemoteDevice.ButtonPressed -= RemoteDevice_ButtonPressed;
                        tempRemoteDevice.StorageModified -= RemoteDevice_StorageModified;
                        tempRemoteDevice.PropertyChanged -= RemoteDevice_PropertyChanged;
                        tempRemoteDevice.WipeFinished -= RemoteDevice_WipeFinished;
                        await tempRemoteDevice.Shutdown();
                        await _metaMessenger.PublishOnServer(new CloseChannelMessage(tempRemoteDevice.Id));
                        tempRemoteDevice.Dispose();
                    }
                    try
                    {
                        await _remoteDeviceMessenger.DisconnectFromServer();
                    }
                    catch (InvalidOperationException)
                    {
                        // IMetaPubSub may throw InvalidOperationException if we try to disconnect without first connecting
                    }
                }
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
                
            IsCreatingRemoteDevice = false;
            IsAuthorizingRemoteDevice = false;
            IsLoadingStorage = false;
            IsStorageLoaded = false;

            NotifyPropertyChanged(nameof(IsAuthorized));
        }

        async Task CreateRemoteDeviceAsync(CancellationToken cancellationToken)
        {
            if (_remoteDevice != null || IsCreatingRemoteDevice)
                return;

            HideezErrorCode initErrorCode = HideezErrorCode.Ok;

            try
            {
                _log.WriteLine($"({SerialNo}) Establishing remote vault connection");

                if (cancellationToken.IsCancellationRequested)
                {
                    initErrorCode = HideezErrorCode.RemoteDeviceCreationCancelled;
                    return;
                }

                _log.WriteLine("Checking for available channels");
                var channelsReply = await _metaMessenger.ProcessOnServer<GetAvailableChannelsMessageReply>(new GetAvailableChannelsMessage(SerialNo));
                var channels = channelsReply.FreeChannels;
                if (channels.Length == 0)
                    throw new Exception(string.Format(TranslationSource.Instance["Vault.Error.NoAvailableChannels"], SerialNo)); // Todo: separate exception type
                var channelNo = channels.FirstOrDefault();
                _log.WriteLine($"{channels.Length} channels available");

                if (cancellationToken.IsCancellationRequested)
                {
                    initErrorCode = HideezErrorCode.RemoteDeviceCreationCancelled;
                    return;
                }

                ShowInfo(string.Format(TranslationSource.Instance["Vault.Notification.PreparingForAuth"], SerialNo));
                IsCreatingRemoteDevice = true;
                _remoteDevice = await _remoteDeviceFactory.CreateRemoteDeviceAsync(NotificationsId, channelNo, _remoteDeviceMessenger);
                _remoteDevice.PropertyChanged += RemoteDevice_PropertyChanged;
                _remoteDevice.WipeFinished += RemoteDevice_WipeFinished;
                
                if (cancellationToken.IsCancellationRequested)
                {
                    initErrorCode = HideezErrorCode.RemoteDeviceCreationCancelled;
                    return;
                }

                await _remoteDevice.VerifyAndInitialize();

                if (cancellationToken.IsCancellationRequested)
                {
                    initErrorCode = HideezErrorCode.RemoteDeviceCreationCancelled;
                    return;
                }

                if (!_remoteDevice.IsInitialized && _remoteDevice.IsErrorState)
                    throw new HideezException(HideezErrorCode.ChannelInitializationFailed);

                if (!_remoteDevice.IsAuthorized)
                    ShowInfo(string.Empty);

                if (_remoteDevice.SerialNo != SerialNo)
                    throw new Exception(TranslationSource.Instance["Vault.Error.InvalidRemoteSerialNo"]);

                _log.WriteLine($"({SerialNo}) Creating password manager");
                PasswordManager = new DevicePasswordManager(_remoteDevice, null);
                _remoteDevice.StorageModified += RemoteDevice_StorageModified;
                _remoteDevice.ButtonPressed += RemoteDevice_ButtonPressed;

                _log.WriteLine($"({SerialNo}) Remote vault connection established");
            }
            catch (HideezException ex)
            {
                _log.WriteLine(ex);
                if(ex.ErrorCode != HideezErrorCode.ChannelInitializationFailed)
                    ShowError(ex.Message);
                initErrorCode = ex.ErrorCode;
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
                ShowError(ex.Message);
                initErrorCode = HideezErrorCode.UnknownError;
            }
            finally
            {
                await _metaMessenger.Publish(new HideDialogMessage(typeof(PinDialog)));

                if (initErrorCode != HideezErrorCode.Ok)
                    await ShutdownRemoteDeviceAsync(initErrorCode);

                IsCreatingRemoteDevice = false;
            }

        }

        async Task AuthorizeRemoteDevice(CancellationToken ct)
        {
            if (_remoteDevice == null || IsAuthorized || IsAuthorizingRemoteDevice)
                return;

            try
            {
                IsAuthorizingRemoteDevice = true;

                if (_applicationMode == ApplicationMode.Standalone)
                    await MasterPasswordWorkflow(ct);

                if (_remoteDevice.AccessLevel.IsLocked)
                    throw new HideezException(HideezErrorCode.DeviceIsLocked);

                else if (_remoteDevice.AccessLevel.IsLinkRequired && _applicationMode == ApplicationMode.Enterprise)
                    throw new HideezException(HideezErrorCode.DeviceNotAssignedToUser);

                await ButtonWorkflow(ct);
                await PinWorkflow(ct);
                await ButtonWorkflow(ct);

                if (ct.IsCancellationRequested)
                    ShowError(string.Format(TranslationSource.Instance["Vault.Error.AuthCanceled"], NotificationsId));
                else if (IsAuthorized)
                    _log.WriteLine($"({_remoteDevice.Id}) Remote vault authorized");
            }
            catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.DeviceIsLocked || ex.ErrorCode == HideezErrorCode.DeviceIsLockedByPin)
            {
                _log.WriteLine($"({Id}) Auth failed. Vault is locked due to too many incorrect PIN entries");
                await _metaMessenger.Publish(new ShowLockNotificationMessage(TranslationSource.Instance["Notification.DeviceLockedByPin.Message"],
                                    TranslationSource.Instance["Notification.DeviceLockedByPin.Caption"],
                                    new NotificationOptions() { CloseTimeout = NotificationOptions.LongTimeout },
                                    Id));
            }
            catch (HideezException ex)
            {
                _log.WriteLine(ex);
                ShowError(ex.Message);
            }
            catch (OperationCanceledException ex)
            {
                _log.WriteLine(ex);
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
                ShowError(ex.Message);
            }
            finally
            {
                await _metaMessenger.Publish(new HideDialogMessage(typeof(PinDialog)));
                await _metaMessenger.Publish(new HideDialogMessage(typeof(MasterPasswordDialog)));

                IsAuthorizingRemoteDevice = false;
            }
        }

        async Task LoadStorage()
        {
            if (_remoteDevice == null || !IsAuthorized || IsLoadingStorage || PasswordManager == null)
                return;

            try
            {
                IsStorageLoaded = false;
                IsLoadingStorage = true;

                _log.WriteLine($"({serialNo}) Loading storage");
                await PasswordManager.Load();
                _log.WriteLine($"({serialNo}) Loaded {PasswordManager.Accounts.Count()} entries from storage");
                
                IsStorageLoaded = true;
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
                ShowError(ex.Message);

                await ShutdownRemoteDeviceAsync(HideezErrorCode.UnknownError);
            }
            finally
            {
                IsLoadingStorage = false;
            }
        }


        async Task<bool> ButtonWorkflow(CancellationToken ct)
        {
            if (!_remoteDevice.AccessLevel.IsButtonRequired)
                return true;

            ShowInfo(TranslationSource.Instance["Vault.Notification.PressButton"]);
            await _metaMessenger.Publish(new ShowButtonConfirmUiMessage(Id));
            var res = await _remoteDevice.WaitButtonConfirmation(CREDENTIAL_TIMEOUT, ct);
            return res;
        }

        Task<bool> PinWorkflow(CancellationToken ct)
        {
            if (_remoteDevice.AccessLevel.IsPinRequired && _remoteDevice.AccessLevel.IsNewPinRequired)
            {
                return SetPinWorkflow(ct);
            }
            else if (_remoteDevice.AccessLevel.IsPinRequired)
            {
                return EnterPinWorkflow(ct);
            }

            return Task.FromResult(true);
        }

        public async Task<bool> SetPinWorkflow(CancellationToken ct)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>> SetPinWorkflow +++++++++++++++++++++++++++++++++++++++");

            bool pinOk = false;
            ShowInfo(TranslationSource.Instance["Vault.Notification.NewPin"]);
            while (AccessLevel.IsNewPinRequired)
            {
                var pinProc = new GetPinProc(_metaMessenger, Id, withConfirm: true);
                var procResult = await pinProc.Run(CREDENTIAL_TIMEOUT, ct);

                if (procResult == null)
                    return false; // procedure timed out or cancelled by user

                var pin = procResult.Pin;

                if (pin.Length == 0)
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    Debug.WriteLine($">>>>>>>>>>>>>>> EMPTY PIN");
                    _log.WriteLine("Received empty PIN");
                    continue;
                }

                var pinResult = await _remoteDevice.SetPin(Encoding.UTF8.GetString(pin)); //this using default timeout for BLE commands
                if (pinResult == HideezErrorCode.Ok)
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> PIN OK");
                    pinOk = true;
                    break;
                }
                else if (pinResult == HideezErrorCode.ERR_PIN_TOO_SHORT)
                {
                    ShowError(TranslationSource.Instance["Vault.Error.PinToShort"]);
                }
                else if (pinResult == HideezErrorCode.ERR_PIN_WRONG)
                {
                    ShowError(TranslationSource.Instance["Vault.Error.InvalidPin"]);
                }
            }
            Debug.WriteLine(">>>>>>>>>>>>>>> SetPinWorkflow ---------------------------------------");
            return pinOk;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>Returns true is pin workflow successful. Returns false if workflow cancelled.</returns>
        /// <exception cref="HideezException">Thrown with code <see cref="HideezErrorCode.DeviceIsLocked"/> if device is locked due to failed attempt</exception>
        async Task<bool> EnterPinWorkflow(CancellationToken ct)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>> EnterPinWorkflow +++++++++++++++++++++++++++++++++++++++");

            bool pinOk = false;
            ShowInfo(TranslationSource.Instance["Vault.Notification.EnterCurrentPin"]);
            while (!AccessLevel.IsLocked)
            {
                var pinProc = new GetPinProc(_metaMessenger, Id);
                var procResult = await pinProc.Run(CREDENTIAL_TIMEOUT, ct);

                if (procResult == null)
                    return false; // procedure timed out or cancelled by user

                var pin = procResult.Pin;

                if (pin.Length == 0)
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    Debug.WriteLine($">>>>>>>>>>>>>>> EMPTY PIN");
                    _log.WriteLine("Received empty PIN");

                    continue;
                }

                var attemptsLeft = PinAttemptsRemain - 1;
                var pinResult = await _remoteDevice.EnterPin(Encoding.UTF8.GetString(pin)); //this using default timeout for BLE commands

                if (pinResult == HideezErrorCode.Ok)
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> PIN OK");
                    pinOk = true;
                    break;
                }
                else if (pinResult == HideezErrorCode.ERR_DEVICE_LOCKED_BY_PIN)
                {
                    throw new HideezException(HideezErrorCode.DeviceIsLocked);
                }
                else // ERR_PIN_WRONG and ERR_PIN_TOO_SHORT should just be displayed as wrong pin for security reasons
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> Wrong PIN ({attemptsLeft} attempts left)");
                    if (AccessLevel.IsLocked)
                        ShowError(TranslationSource.Instance["Vault.Error.Pin.LockedByInvalidAttempts"]);
                    else
                    {
                        if (attemptsLeft > 1)
                            ShowError(string.Format(TranslationSource.Instance["Vault.Error.InvalidPin.ManyAttemptsLeft"], attemptsLeft));
                        else
                            ShowError(string.Format(TranslationSource.Instance["Vault.Error.InvalidPin.OneAttemptLeft"]));
                        await _remoteDevice.RefreshDeviceInfo(); // Remaining pin attempts update is not quick enough 
                    }
                }
            }
            Debug.WriteLine(">>>>>>>>>>>>>>> PinWorkflow ------------------------------");
            return pinOk;
        }

        public async Task<bool> ChangePinWorkflow()
        {
            using (var cts = new CancellationTokenSource())
            {
                void ctsCancel(object sender, EventArgs e) { cts.Cancel(); }
                try
                {
                    RemoteDeviceShutdown += ctsCancel;

                    try
                    {
                        while (IsAuthorized)
                        {
                            var proc = new GetPinProc(_metaMessenger, Id, true, true);
                            var procResult = await proc.Run(CREDENTIAL_TIMEOUT, cts.Token);

                            if (procResult == null)
                                return false; // finished by timeout from the _ui.GetPin

                            if (procResult.Pin.Length == 0 || procResult.OldPin.Length == 0)
                            {
                                // we received an empty PIN from the user. Trying again with the same timeout.
                                continue;
                            }

                            var attemptsLeft = PinAttemptsRemain - 1;
                            var newPin = Encoding.UTF8.GetString(procResult.Pin);
                            var oldPin = Encoding.UTF8.GetString(procResult.OldPin);
                            var setPinResult = await _remoteDevice.SetPin(newPin, oldPin);

                            if (setPinResult == HideezErrorCode.Ok)
                            {
                                return true;
                            }
                            else if (setPinResult == HideezErrorCode.ERR_DEVICE_LOCKED_BY_PIN)
                            {
                                throw new HideezException(HideezErrorCode.DeviceIsLocked);
                            }
                            else // ERR_PIN_WRONG and ERR_PIN_TOO_SHORT should just be displayed as wrong pin for security reasons
                            {
                                if (AccessLevel.IsLocked)
                                    ShowError(TranslationSource.Instance["Vault.Error.Pin.LockedByInvalidAttempts"]);
                                else
                                {
                                    if (attemptsLeft > 1)
                                        ShowError(string.Format(TranslationSource.Instance["Vault.Error.InvalidPin.ManyAttemptsLeft"], attemptsLeft));
                                    else
                                        ShowError(string.Format(TranslationSource.Instance["Vault.Error.InvalidPin.OneAttemptLeft"]));
                                    await _remoteDevice.RefreshDeviceInfo(); // Remaining pin attempts update is not quick enough 
                                }
                            }
                        }
                    }
                    catch (HideezException ex)
                    {
                        _log.WriteLine(ex);
                        ShowError(ex.Message);
                    }
                    catch (OperationCanceledException ex)
                    {
                        _log.WriteLine(ex);
                    }
                    catch (Exception ex)
                    {
                        _log.WriteLine(ex);
                        ShowError(ex.Message);
                    }
                    finally
                    {
                        await _metaMessenger.Publish(new HideDialogMessage(typeof(PinDialog)));
                    }

                    return false;
                }
                finally
                {
                    RemoteDeviceShutdown -= ctsCancel;
                }
            }
        }

        Task<bool> MasterPasswordWorkflow(CancellationToken ct)
        {
            if (_remoteDevice.AccessLevel.IsLinkRequired)
            {
                return SetMasterkeyWorkflow(ct);
            }
            else if (_remoteDevice.AccessLevel.IsMasterKeyRequired)
            {
                return EnterMasterkeyWorkflow(ct);
            }
            else if (_remoteDevice.IsLocked)
                return UnlockDevice(ct);

            return Task.FromResult(true);
        }

        async Task<bool> SetMasterkeyWorkflow(CancellationToken ct)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>> SetMasterkeyWorkflow +++++++++++++++++++++++++++++++++++++++");

            bool passwordOk = false;
            ShowInfo(TranslationSource.Instance["Vault.Notification.NewMasterPassword"]);
            while (AccessLevel.IsLinkRequired)
            {
                var mpProc = new GetMasterPasswordProc(_metaMessenger, Id, withConfirm: true);
                var procResult = await mpProc.Run(CREDENTIAL_TIMEOUT, ct);

                if (procResult == null)
                    return false; 

                var masterPassword = procResult.Password;

                if (masterPassword.Length == 0)
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    Debug.WriteLine($">>>>>>>>>>>>>>> EMPTY Masterkey");
                    _log.WriteLine("Received empty Masterkey");
                    continue;
                }

                if (masterPassword.Length < 8)
                {
                    ShowError(TranslationSource.Instance["Vault.Error.MasterPasswordToShort"]);
                    continue;
                }

                var masterkey = MasterPasswordConverter.GetMasterKey(masterPassword, SerialNo);

                try
                {
                    var code = Encoding.UTF8.GetBytes("123456");

                    await _remoteDevice.Link(masterkey, code, 3);
                    await _remoteDevice.RefreshDeviceInfo();

                    if (_remoteDevice.AccessLevel.IsLocked)
                    {
                        await _remoteDevice.UnlockDeviceCode(code);
                        await _remoteDevice.RefreshDeviceInfo();
                    }

                    await _remoteDevice.Access(DateTime.Now, masterkey, new AccessParams()
                    {
                        MasterKey_Bond = true
                    });

                    await _remoteDevice.RefreshDeviceInfo();
                }
                catch(Exception ex)
                {
                    ShowError(ex.Message);
                    continue;
                }
                
                _log.WriteLine(">>>>>>>>>>>>>>> Masterkey OK");
                passwordOk = true;
                break;
            }
            Debug.WriteLine(">>>>>>>>>>>>>>> SetMasterkeyWorkflow ---------------------------------------");
            return passwordOk;
        }

        async Task<bool> EnterMasterkeyWorkflow(CancellationToken ct)
        {
            Debug.WriteLine(">>>>>>>>>>>>>>> EnterMasterkeyWorkflow +++++++++++++++++++++++++++++++++++++++");

            bool passwordOk = false;
            ShowInfo(TranslationSource.Instance["Vault.Notification.EnterCurrentMasterPassword"]);
            while (AccessLevel.IsMasterKeyRequired)
            {
                var mpProc = new GetMasterPasswordProc(_metaMessenger, Id);
                var procResult = await mpProc.Run(CREDENTIAL_TIMEOUT, ct);

                if (procResult == null)
                    return false;

                var masterPassword = procResult.Password;

                if (masterPassword.Length == 0)
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    Debug.WriteLine($">>>>>>>>>>>>>>> EMPTY Masterkey");
                    _log.WriteLine("Received empty Masterkey");

                    continue;
                }

                var masterkey = MasterPasswordConverter.GetMasterKey(masterPassword, SerialNo);

                try
                {
                    await _remoteDevice.CheckPassphrase(masterkey); //this using default timeout for BLE commands
                    await _remoteDevice.RefreshDeviceInfo();
                }
                catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.ERR_KEY_WRONG)
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> Wrong masterkey ");
                    ShowError(TranslationSource.Instance["Vault.Error.MasterPass.InvalidMasterPass"]);
                    continue;
                }
                catch (Exception ex)
                {
                    ShowError(ex.Message);
                    continue;
                }

                _log.WriteLine(">>>>>>>>>>>>>>> Masterkey OK");
                passwordOk = true;
                break;
            }
            Debug.WriteLine(">>>>>>>>>>>>>>> EnterMasterkeyWorkflow ------------------------------");
            return passwordOk;
        }

        async Task<bool> UnlockDevice(CancellationToken ct)
        {
            bool passwordOk = false;
            ShowInfo(TranslationSource.Instance["Vault.Notification.UnlockDevice"]);
            while (AccessLevel.IsLocked)
            {
                var mpProc = new GetMasterPasswordProc(_metaMessenger, Id);
                var procResult = await mpProc.Run(CREDENTIAL_TIMEOUT, ct);

                if (procResult == null)
                    return false;

                var masterPassword = procResult.Password;

                if (masterPassword.Length == 0)
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    Debug.WriteLine($">>>>>>>>>>>>>>> EMPTY Masterkey");
                    _log.WriteLine("Received empty Masterkey");

                    continue;
                }

                var masterkey = MasterPasswordConverter.GetMasterKey(masterPassword, SerialNo);

                try
                {
                    await _remoteDevice.Unlock(masterkey); //this using default timeout for BLE commands
                    await _remoteDevice.RefreshDeviceInfo();
                }
                catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.ERR_KEY_WRONG)
                {
                    Debug.WriteLine($">>>>>>>>>>>>>>> Wrong masterkey ");
                    ShowError(TranslationSource.Instance["Vault.Error.MasterPass.InvalidMasterPass"]);
                    continue;
                }
                catch (Exception ex)
                {
                    ShowError(ex.Message);
                    continue;
                }

                _log.WriteLine(">>>>>>>>>>>>>>> Masterkey OK");
                passwordOk = true;
                break;
            }
            return passwordOk;
        }

        public async Task<bool> ChangeMasterkeyWorkflow()
        {
            using (var cts = new CancellationTokenSource())
            {
                void ctsCancel(object sender, EventArgs e) { cts.Cancel(); }
                try
                {
                    RemoteDeviceShutdown += ctsCancel;

                    try
                    {
                        while (IsAuthorized)
                        {
                            var proc = new GetMasterPasswordProc(_metaMessenger, Id, true, true);
                            var procResult = await proc.Run(CREDENTIAL_TIMEOUT, cts.Token);

                            if (procResult == null)
                                return false; // finished by timeout from the _ui.GetPin

                            if (procResult.Password.Length == 0 || procResult.OldPassword.Length == 0)
                            {
                                // we received an empty password from the user. Trying again with the same timeout.
                                continue;
                            }

                            var oldPass = MasterPasswordConverter.GetMasterKey(procResult.OldPassword, _remoteDevice.SerialNo);
                            var newPass = MasterPasswordConverter.GetMasterKey(procResult.Password, _remoteDevice.SerialNo);

                            var changePassphraseResult = await _remoteDevice.ChangePassphrase(oldPass, newPass);

                            if (changePassphraseResult == HideezErrorCode.Ok)
                            {
                                return true;
                            }
                            else if (changePassphraseResult == HideezErrorCode.ERR_KEY_WRONG)
                            {
                                ShowError(TranslationSource.Instance["Vault.Error.MasterPass.InvalidMasterPass"]);
                                // There is no limit to the amount of retries
                            }
                        }
                    }
                    catch (HideezException ex)
                    {
                        _log.WriteLine(ex);
                        ShowError(ex.Message);
                    }
                    catch (OperationCanceledException ex)
                    {
                        _log.WriteLine(ex);
                    }
                    catch (Exception ex)
                    {
                        _log.WriteLine(ex);
                        ShowError(ex.Message);
                    }
                    finally
                    {
                        await _metaMessenger.Publish(new HideDialogMessage(typeof(MasterPasswordDialog)));
                    }

                    return false;
                }
                finally
                {
                    RemoteDeviceShutdown -= ctsCancel;
                }
            }
        }

        public async Task<AccessParams> GetAccessProfile()
        {
            var reply = await _remoteDevice.GetAccessProfile();
            return reply.AccessParams;
        }

        public async Task<bool> ChangeAccessProfileWorkflow(bool requirePin, bool requireButton, int expirationSeconds)
        {
            using (var cts = new CancellationTokenSource())
            {
                void ctsCancel(object sender, EventArgs e) { cts.Cancel(); }
                try
                {
                    RemoteDeviceShutdown += ctsCancel;

                    while (IsAuthorized)
                    {
                        try
                        {
                            var masterPasswordProc = new GetMasterPasswordProc(_metaMessenger, Id);
                            var procResult = await masterPasswordProc.Run(CREDENTIAL_TIMEOUT, cts.Token);

                            if (procResult == null)
                                return false;

                            if (procResult.Password.Length == 0)
                                continue;

                            var masterPassword = procResult.Password;

                            var accessParams = new AccessParams
                            {
                                MasterKey_Bond = true,
                                MasterKey_Channel = false,
                                MasterKey_Connect = false,
                                MasterKeyExpirationPeriod = !requirePin && !requireButton ? expirationSeconds : 0,

                                Pin_Bond = requirePin,
                                Pin_Channel = requirePin,
                                Pin_Connect = requirePin,
                                PinMaxTries = 5,
                                PinMinLength = requirePin ? 4 : 0,

                                Button_Bond = requireButton,
                                Button_Channel = requireButton,
                                Button_Connect = requireButton,

                                PinExpirationPeriod = requirePin || requireButton ? expirationSeconds : 0,
                            };

                            var masterKey = MasterPasswordConverter.GetMasterKey(masterPassword, SerialNo);

                            await _remoteDevice.Access(DateTime.UtcNow, masterKey, accessParams);
                            await _remoteDevice.RefreshDeviceInfo();

                            return true;
                        }
                        catch (HideezException ex)
                        {
                            _log.WriteLine(ex);
                            ShowError(ex.Message);
                        }
                        catch (OperationCanceledException ex)
                        {
                            _log.WriteLine(ex);
                        }
                        catch (Exception ex)
                        {
                            _log.WriteLine(ex);
                            ShowError(ex.Message);
                        }
                        finally
                        {
                            await _metaMessenger.Publish(new HideDialogMessage(typeof(MasterPasswordDialog)));
                        }
                    }

                    return false;
                }
                finally
                {
                    RemoteDeviceShutdown -= ctsCancel;
                }
            }
        }

        public async Task<bool> WipeWorkflow()
        {
            using (var cts = new CancellationTokenSource())
            {
                void ctsCancel(object sender, EventArgs e) { cts.Cancel(); }
                try
                {
                    RemoteDeviceShutdown += ctsCancel;

                    try
                    {
                        var confirmationProc = new GetUserWipeConfirmationProc(_metaMessenger, Id);
                        var confirmProcResult = await confirmationProc.Run(30_000, cts.Token);

                        if (confirmProcResult == null || !confirmProcResult.Confirmed)
                            return false;

                        var reply = await _remoteDevice.Wipe(new byte[0]);

                        var msg = await _metaMessenger.When<WipeFinishedMessage>(30_000, cancellationToken:cts.Token);

                        return true;
                    }
                    catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.BleSendError)
                    {
                        _log.WriteLine(ex.Message, LogErrorSeverity.Warning);
                    }
                    catch (HideezException ex)
                    {
                        _log.WriteLine(ex);
                        ShowError(ex.Message);
                    }
                    catch (OperationCanceledException ex)
                    {
                        _log.WriteLine(ex.Message);
                    }
                    catch (TimeoutException ex)
                    {
                        _log.WriteLine(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _log.WriteLine(ex);
                        ShowError(ex.Message);
                    }
                    finally
                    {
                        await _metaMessenger.Publish(new HideDialogMessage(typeof(WipeDialog)));
                    }

                    return false;
                }
                finally
                {
                    RemoteDeviceShutdown -= ctsCancel;
                }
            }
        }

        #region Notifications display
        void ShowInfo(string message)
        {
            _metaMessenger?.Publish(new ShowInfoNotificationMessage(message, notificationId: NotificationsId));
        }

        void ShowError(string message)
        {
            _metaMessenger?.Publish(new ShowErrorNotificationMessage(message, notificationId: NotificationsId));
        }

        void ShowWarn(string message)
        {
            _metaMessenger?.Publish(new ShowWarningNotificationMessage(message, notificationId: NotificationsId));
        }
        #endregion

        #region IDisposable Support
        bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    PropertyChanged -= Device_PropertyChanged;
                    _metaMessenger.Unsubscribe<HideezMiddleware.IPC.Messages.DeviceConnectionStateChangedMessage>(OnDeviceConnectionStateChanged);
                    _metaMessenger.Unsubscribe<HideezMiddleware.IPC.Messages.DeviceInitializedMessage>(OnDeviceInitialized);
                    _metaMessenger.Unsubscribe<HideezMiddleware.IPC.Messages.DeviceFinishedMainFlowMessage>(OnDeviceFinishedMainFlow);
                    _metaMessenger.Unsubscribe<HideezMiddleware.IPC.Messages.DeviceProximityChangedMessage>(OnDeviceProximityChanged);
                    _metaMessenger.Unsubscribe<HideezMiddleware.IPC.Messages.DeviceBatteryChangedMessage>(OnDeviceBatteryChanged);
                    _metaMessenger.Unsubscribe<HideezMiddleware.IPC.Messages.DeviceProximityLockEnabledMessage>(OnDeviceProximityLockEnabled);
                    _metaMessenger.Unsubscribe<HideezMiddleware.IPC.Messages.LockDeviceStorageMessage>(OnLockDeviceStorage);
                    _metaMessenger.Unsubscribe<HideezMiddleware.IPC.Messages.LiftDeviceStorageLockMessage>(OnLiftDeviceStorageLock);
                    _metaMessenger.Unsubscribe<SessionSwitchMessage>(OnSessionSwitch);
                    StopDeviceMessengerAsync();
                }

                disposed = true;
            }
        }

        ~DeviceModel()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        async void StopDeviceMessengerAsync()
        {
            try
            {
                await _remoteDeviceMessenger.DisconnectFromServer();
            }
            catch (InvalidOperationException)
            {
                // IMetaPubSub may throw InvalidOperationException if we try to disconnect without first connecting
                // At the moment there is no reliable way to see if that is the case
                // Therefore InvalidOperationException is handled silently
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
        }
        #endregion
    }
}
