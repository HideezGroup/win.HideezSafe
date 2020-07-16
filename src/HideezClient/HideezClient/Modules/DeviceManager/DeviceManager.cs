﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Messaging;
using HideezClient.Messages;
using HideezClient.Modules.ServiceProxy;
using System.Linq;
using HideezClient.Models;
using System.ServiceModel;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using HideezClient.Controls;
using Hideez.SDK.Communication;
using HideezMiddleware.Threading;
using Hideez.SDK.Communication.Log;
using HideezClient.Modules.Log;
using HideezMiddleware.IPC.DTO;

namespace HideezClient.Modules.DeviceManager
{
    class DeviceManager : IDeviceManager
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger(nameof(DeviceManager));
        private readonly IMessenger _messenger;
        private readonly IServiceProxy _serviceProxy;
        private readonly IWindowsManager _windowsManager;
        readonly IRemoteDeviceFactory _remoteDeviceFactory;
        readonly SemaphoreQueue _semaphoreQueue = new SemaphoreQueue(1, 1);
        ConcurrentDictionary<string, Device> _devices { get; } = new ConcurrentDictionary<string, Device>();

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

        public DeviceManager(IMessenger messenger, IServiceProxy serviceProxy,
            IWindowsManager windowsManager, IRemoteDeviceFactory remoteDeviceFactory)
        {
            _messenger = messenger;
            _serviceProxy = serviceProxy;
            _windowsManager = windowsManager;
            _remoteDeviceFactory = remoteDeviceFactory;

            _messenger.Register<DevicesCollectionChangedMessage>(this, OnDevicesCollectionChanged);
            _messenger.Register<DeviceConnectionStateChangedMessage>(this, OnDeviceConnectionStateChanged);

            _serviceProxy.Disconnected += OnServiceProxyConnectionStateChanged;
            _serviceProxy.Connected += OnServiceProxyConnectionStateChanged;
        }

        public IEnumerable<Device> Devices => _devices.Values;

        async void OnServiceProxyConnectionStateChanged(object sender, EventArgs e)
        {
            await _semaphoreQueue.WaitAsync();
            try
            {
                if (!_serviceProxy.IsConnected)
                    await ClearDevicesCollection();
                else
                    await EnumerateDevices();
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
            finally
            {
                _semaphoreQueue.Release();
            }
        }

        async void OnDevicesCollectionChanged(DevicesCollectionChangedMessage message)
        {
            await _semaphoreQueue.WaitAsync();
            try
            {
                await EnumerateDevices(message.Devices);
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
            finally
            {
                _semaphoreQueue.Release();
            }
        }

        async void OnDeviceConnectionStateChanged(DeviceConnectionStateChangedMessage message)
        {
            await _semaphoreQueue.WaitAsync();
            try
            {
                await EnumerateDevices();
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
            finally
            {
                _semaphoreQueue.Release();
            }
        }

        async Task ClearDevicesCollection()
        {
            foreach (var dvm in Devices.ToArray())
                await RemoveDevice(dvm);
        }

        async Task EnumerateDevices()
        {
            try
            {
                // TODO: FIX THIS LINE
                //var serviceDevices = await _serviceProxy.GetService().GetDevicesAsync();
                //await EnumerateDevices(serviceDevices);
            }
            catch (FaultException<HideezServiceReference.HideezServiceFault> ex)
            {
                _log.WriteLine(ex.FormattedMessage(), LogErrorSeverity.Error);
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
        }

        async Task EnumerateDevices(DeviceDTO[] serviceDevices)
        {
            try
            {
                // Ignore devices that are not connected
                serviceDevices = serviceDevices.Where(d => d.IsConnected).ToArray();

                // Create device if it does not exist in UI
                foreach (var deviceDto in serviceDevices)
                    AddDevice(deviceDto);

                // delete device from UI if its deleted from service
                Device[] missingDevices = _devices.Values.Where(d => serviceDevices.FirstOrDefault(dto => dto.SerialNo == d.SerialNo) == null).ToArray();
                await RemoveDevices(missingDevices);
            }
            catch (FaultException<HideezServiceReference.HideezServiceFault> ex)
            {
                _log.WriteLine(ex.FormattedMessage(), LogErrorSeverity.Error);
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);
            }
        }

        void AddDevice(DeviceDTO dto)
        {
            if (!_devices.ContainsKey(dto.Id))
            {
                var device = new Device(_serviceProxy, _remoteDeviceFactory, _messenger, dto);
                device.PropertyChanged += Device_PropertyChanged;

                if (_devices.TryAdd(device.Id, device))
                {
                    DevicesCollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, device));
                }
            }
        }

        private void Device_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(sender is Device device && e.PropertyName == nameof(Device.IsLoadingStorage) && device.IsLoadingStorage)
            {
                CredentialsLoadNotificationViewModel viewModal = new CredentialsLoadNotificationViewModel(device);
                _windowsManager.ShowCredentialsLoading(viewModal);
            }
        }

        async Task RemoveDevice(Device device)
        {
            if (_devices.TryRemove(device.Id, out Device removedDevice))
            {
                removedDevice.PropertyChanged -= Device_PropertyChanged;
                await removedDevice.ShutdownRemoteDeviceAsync(HideezErrorCode.DeviceRemoved);
                removedDevice.Dispose();
                DevicesCollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, device));
            }
        }

        async Task RemoveDevices(Device[] devices)
        {
            foreach (var device in devices)
                await RemoveDevice(device);
        }
    }
}
