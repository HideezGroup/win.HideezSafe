﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Messaging;
using HideezClient.HideezServiceReference;
using HideezClient.Messages;
using HideezClient.Modules.ServiceProxy;
using NLog;
using System.Linq;
using HideezClient.Models;
using System.ServiceModel;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using HideezClient.Controls;

namespace HideezClient.Modules.DeviceManager
{
    class DeviceManager : IDeviceManager
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IServiceProxy _serviceProxy;
        private readonly IWindowsManager _windowsManager;
        readonly IRemoteDeviceFactory _remoteDeviceFactory;
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
            _serviceProxy = serviceProxy;
            _windowsManager = windowsManager;
            _remoteDeviceFactory = remoteDeviceFactory;

            messenger.Register<DevicesCollectionChangedMessage>(this, OnDevicesCollectionChanged);
            messenger.Register<DeviceInitializedMessage>(this, OnDeviceInitialized);
            messenger.Register<DeviceConnectionStateChangedMessage>(this, OnDeviceConnectionStateChanged);

            _serviceProxy.Disconnected += OnServiceProxyConnectionStateChanged;
            _serviceProxy.Connected += OnServiceProxyConnectionStateChanged;
        }

        public IEnumerable<Device> Devices => _devices.Values;

        async void OnServiceProxyConnectionStateChanged(object sender, EventArgs e)
        {
            try
            {
                if (!_serviceProxy.IsConnected)
                    ClearDevicesCollection();
                else
                    await EnumerateDevices();
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
        }

        void OnDevicesCollectionChanged(DevicesCollectionChangedMessage message)
        {
            Task.Run(() =>
            {
                try
                {
                    EnumerateDevices(message.Devices);
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                }
            });
        }

        void OnDeviceConnectionStateChanged(DeviceConnectionStateChangedMessage message)
        {
            Task.Run(() =>
            {
                try
                {
                    if (_devices.TryGetValue(message.Device.Id, out Device dvm))
                    {
                        dvm.LoadFrom(message.Device);

                        if (message.Device.IsInitialized && dvm.IsConnected)
                            _ = TryCreateRemoteDeviceAsync(dvm);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                }
            });
        }

        void OnDeviceInitialized(DeviceInitializedMessage message)
        {
            Task.Run(() =>
            {
                try
                {
                    if (_devices.TryGetValue(message.Device.Id, out Device dvm))
                    {
                        dvm.LoadFrom(message.Device);

                        if (message.Device.IsInitialized && dvm.IsConnected)
                            _ = TryCreateRemoteDeviceAsync(dvm);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                }
            });
        }

        void ClearDevicesCollection()
        {
            foreach (var dvm in Devices.ToArray())
                RemoveDevice(dvm);
        }

        async Task EnumerateDevices()
        {
            try
            {
                var serviceDevices = await _serviceProxy.GetService().GetDevicesAsync();
                EnumerateDevices(serviceDevices);
            }
            catch (FaultException<HideezServiceFault> ex)
            {
                _log.Error(ex.FormattedMessage());
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
        }

        void EnumerateDevices(DeviceDTO[] serviceDevices)
        {
            try
            {
                // Create device if it does not exist in UI
                foreach (var deviceDto in serviceDevices)
                    AddDevice(deviceDto);


                // delete device from UI if its deleted from service
                Device[] missingDevices = _devices.Values.Where(d => serviceDevices.FirstOrDefault(dto => dto.SerialNo == d.SerialNo) == null).ToArray();
                RemoveDevices(missingDevices);
            }
            catch (FaultException<HideezServiceFault> ex)
            {
                _log.Error(ex.FormattedMessage());
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }
        }

        void AddDevice(DeviceDTO dto)
        {
            var device = new Device(_serviceProxy, _remoteDeviceFactory);
            device.PropertyChanged += Device_PropertyChanged;

            device.LoadFrom(dto);

            if (_devices.TryAdd(device.Id, device))
            {
                if (dto.IsInitialized && device.IsConnected)
                    _ = TryCreateRemoteDeviceAsync(device); // Fire and forget

                DevicesCollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, device));
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

        void RemoveDevice(Device device)
        {
            if (_devices.TryRemove(device.Id, out Device removedDevice))
            {
                removedDevice.PropertyChanged -= Device_PropertyChanged;
                removedDevice.CloseRemoteDeviceConnection();
                DevicesCollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, device));
            }
        }

        void RemoveDevices(Device[] devices)
        {
            foreach (var device in devices)
                RemoveDevice(device);
        }

        async Task TryCreateRemoteDeviceAsync(Device device)
        {
            if (!device.IsInitialized && !device.IsInitializing)
                await device.EstablishRemoteDeviceConnection();
        }
    }
}
