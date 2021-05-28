using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Linq;
using HideezClient.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using HideezClient.Controls;
using Hideez.SDK.Communication;
using HideezMiddleware.Threading;
using Hideez.SDK.Communication.Log;
using HideezClient.Modules.Log;
using HideezMiddleware.IPC.DTO;
using Meta.Lib.Modules.PubSub;
using Meta.Lib.Modules.PubSub.Messages;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.ApplicationModeProvider;

namespace HideezClient.Modules.DeviceManager
{
    class DeviceManager : IDeviceManager
    {
        readonly Logger _log = LogManager.GetCurrentClassLogger(nameof(DeviceManager));
        readonly IWindowsManager _windowsManager;
        readonly IMetaPubSub _metaMessenger;
        readonly IRemoteDeviceFactory _remoteDeviceFactory;
        readonly ApplicationMode _applicationMode;
        readonly SemaphoreQueue _semaphoreQueue = new SemaphoreQueue(1, 1);
        ConcurrentDictionary<string, DeviceModel> _devices { get; } = new ConcurrentDictionary<string, DeviceModel>();

        // Custom dispatcher is required for unit tests because during test 
        // runs the Application.Current property is null
        Dispatcher Dispatcher
        {
            get
            {
                if (Application.Current != null)
                {
                    return Application.Current.Dispatcher;
                }

                return Dispatcher.CurrentDispatcher;
            }
        }

        public event NotifyCollectionChangedEventHandler DevicesCollectionChanged;

        public DeviceManager(IMetaPubSub metaMessenger, IWindowsManager windowsManager, IRemoteDeviceFactory remoteDeviceFactory, IApplicationModeProvider applicationModeProvider)
        {
            _windowsManager = windowsManager;
            _remoteDeviceFactory = remoteDeviceFactory;
            _metaMessenger = metaMessenger;

            _applicationMode = applicationModeProvider.GetApplicationMode();

            _metaMessenger.TrySubscribeOnServer<DevicesCollectionChangedMessage>(OnDevicesCollectionChanged);
            _metaMessenger.TrySubscribeOnServer<DeviceConnectionStateChangedMessage>(OnDeviceConnectionStateChanged);

            _metaMessenger.Subscribe<DisconnectedFromServerEvent>(OnDisconnectedFromService, null);
        }

        public DeviceManager(IMetaPubSub metaMessenger, IWindowsManager windowsManager, IRemoteDeviceFactory remoteDeviceFactory, IEnumerable<DeviceDTO> devices, IApplicationModeProvider applicationModeProvider)
            :this(metaMessenger,windowsManager, remoteDeviceFactory, applicationModeProvider)
        {
            foreach (var device in devices)
                AddDevice(device);
        }

        public IEnumerable<DeviceModel> Devices => _devices.Values;

        Task OnDisconnectedFromService(DisconnectedFromServerEvent arg)
        {
            TryHandleDisconnectFromService();
            return Task.CompletedTask;
        }

        void TryHandleDisconnectFromService()
        {
            // Fire & Forget to avoid blocking MetaPubSub while we are handling message
            Task.Run(async () =>
            {
                await _semaphoreQueue.WaitAsync();
                try
                {
                    await ClearDevicesCollection();
                }
                catch (Exception ex)
                {
                    _log.WriteLine(ex);
                }
                finally
                {
                    _semaphoreQueue.Release();
                }
            });
        }

        Task OnDevicesCollectionChanged(DevicesCollectionChangedMessage message)
        {
            // Fire & Forget to avoid blocking MetaPubSub while we are handling message
            TryHandleDevicesCollectionChange(message);
            return Task.CompletedTask;
        }

        void TryHandleDevicesCollectionChange(DevicesCollectionChangedMessage message)
        {
            // Fire & Forget to avoid blocking MetaPubSub while we are handling message
            Task.Run(async () =>
            {
                await _semaphoreQueue.WaitAsync();
                try
                {
                    // Ignore devices that are not connected
                    var serviceDevices = message.Devices.Where(d => d.IsConnected).ToArray();

                    // Create device if it does not exist in UI
                    foreach (var deviceDto in serviceDevices)
                        AddDevice(deviceDto);

                    // delete device from UI if its deleted from service
                    DeviceModel[] missingDevices = _devices.Values.Where(d => serviceDevices.FirstOrDefault(dto => dto.Id == d.Id) == null).ToArray();
                    await RemoveDevices(missingDevices);
                }
                catch (Exception ex)
                {
                    _log.WriteLine(ex);
                }
                finally
                {
                    _semaphoreQueue.Release();
                }
            });
        }

        Task OnDeviceConnectionStateChanged(DeviceConnectionStateChangedMessage message)
        {
            TryHandleDeviceStateChange(message);
            return Task.CompletedTask;
        }

        void TryHandleDeviceStateChange(DeviceConnectionStateChangedMessage message)
        {
            // Fire & Forget to avoid blocking MetaPubSub while we are handling message
            Task.Run(async () =>
            {
                await _semaphoreQueue.WaitAsync();
                try
                {
                    if (message.Device.IsConnected)
                    {
                        // Add device if its connected and is missing from devices collection
                        AddDevice(message.Device);
                    }
                    else
                    {
                        // Remove device from collection if its not connected
                        var device = _devices.Values.FirstOrDefault(d => d.Id == message.Device.Id);
                        if (device != null)
                            await RemoveDevice(device);
                    }
                }
                catch (Exception ex)
                {
                    _log.WriteLine(ex);
                }
                finally
                {
                    _semaphoreQueue.Release();
                }
            });
        }

        async Task ClearDevicesCollection()
        {
            foreach (var dvm in Devices.ToArray())
                await RemoveDevice(dvm);
        }

        void AddDevice(DeviceDTO dto)
        {
            if (dto.ChannelNo == 1 && !_devices.ContainsKey(dto.Id))
            {
                var device = new DeviceModel(_remoteDeviceFactory, _metaMessenger, dto, _applicationMode);
                device.PropertyChanged += Device_PropertyChanged;

                if (_devices.TryAdd(device.Id, device))
                {
                    DevicesCollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, device));
                }
            }
        }

        void Device_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(sender is DeviceModel device && e.PropertyName == nameof(DeviceModel.IsLoadingStorage) && device.IsLoadingStorage)
            {
                CredentialsLoadNotificationViewModel viewModal = new CredentialsLoadNotificationViewModel(device);
                _windowsManager.ShowCredentialsLoading(viewModal);
            }
        }

        async Task RemoveDevice(DeviceModel device)
        {
            if (_devices.TryRemove(device.Id, out DeviceModel removedDevice))
            {
                removedDevice.PropertyChanged -= Device_PropertyChanged;
                await removedDevice.ShutdownRemoteDeviceAsync(HideezErrorCode.DeviceRemoved);
                removedDevice.Dispose();
                DevicesCollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, device));
            }
        }

        async Task RemoveDevices(DeviceModel[] devices)
        {
            foreach (var device in devices)
                await RemoveDevice(device);
        }
    }
}
