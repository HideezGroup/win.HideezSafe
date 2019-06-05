﻿using System.Collections.Generic;
using System.ServiceModel;

namespace ServiceLibrary
{
    [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ICallbacks))]
    public interface IHideezService
    {
        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        bool AttachClient(ServiceClientParameters parameters);

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void DetachClient();

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        int Ping();

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void Shutdown();

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        bool GetAdapterState(Adapter adapter);

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        DeviceDTO[] GetPairedDevices();

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void EnableMonitoringProximity(string deviceId);

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void DisableMonitoringProximity(string deviceId);

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void EnableMonitoringDeviceProperties(string deviceId);

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void DisableMonitoringDeviceProperties(string deviceId);

        // Contract is only for testconsole and hostservice
        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void OnSessionChange(bool sessionLocked);

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void SaveCredential(string deviceId, string login, string password);

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void DisconnectDevice(string deviceId);

        [OperationContract]
        [FaultContract(typeof(HideezServiceFault))]
        void RemoveDevice(string deviceId);
    }

    public interface ICallbacks
    {
        [OperationContract(IsOneWay = true)]
        void LockWorkstationRequest();

        [OperationContract(IsOneWay = true)]
        void ActivateWorkstationScreenRequest();

        [OperationContract(IsOneWay = true)]
        void HESConnectionStateChanged(bool isConnected);

        [OperationContract(IsOneWay = true)]
        void RFIDConnectionStateChanged(bool isConnected);

        [OperationContract(IsOneWay = true)]
        void DongleConnectionStateChanged(bool isConnected);

        [OperationContract(IsOneWay = true)]
        void PairedDevicesCollectionChanged(DeviceDTO[] devices);

        [OperationContract(IsOneWay = true)]
        void PairedDevicePropertyChanged(DeviceDTO device);

        [OperationContract(IsOneWay = true)]
        void ProximityChanged(string deviceId, double proximity);
    }

    public enum Adapter
    {
        HES,
        RFID,
        Dongle,
    }
}
