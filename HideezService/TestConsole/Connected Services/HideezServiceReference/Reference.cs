﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TestConsole.HideezServiceReference {
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    [System.ServiceModel.ServiceContractAttribute(ConfigurationName="HideezServiceReference.IHideezService", CallbackContract=typeof(TestConsole.HideezServiceReference.IHideezServiceCallback), SessionMode=System.ServiceModel.SessionMode.Required)]
    public interface IHideezService {
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IHideezService/AttachClient", ReplyAction="http://tempuri.org/IHideezService/AttachClientResponse")]
        [System.ServiceModel.FaultContractAttribute(typeof(ServiceLibrary.HideezServiceFault), Action="http://tempuri.org/IHideezService/AttachClientHideezServiceFaultFault", Name="HideezServiceFault", Namespace="http://schemas.datacontract.org/2004/07/ServiceLibrary")]
        bool AttachClient(ServiceLibrary.ServiceClientParameters parameters);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IHideezService/AttachClient", ReplyAction="http://tempuri.org/IHideezService/AttachClientResponse")]
        System.Threading.Tasks.Task<bool> AttachClientAsync(ServiceLibrary.ServiceClientParameters parameters);
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IHideezService/DetachClient", ReplyAction="http://tempuri.org/IHideezService/DetachClientResponse")]
        [System.ServiceModel.FaultContractAttribute(typeof(ServiceLibrary.HideezServiceFault), Action="http://tempuri.org/IHideezService/DetachClientHideezServiceFaultFault", Name="HideezServiceFault", Namespace="http://schemas.datacontract.org/2004/07/ServiceLibrary")]
        void DetachClient();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IHideezService/DetachClient", ReplyAction="http://tempuri.org/IHideezService/DetachClientResponse")]
        System.Threading.Tasks.Task DetachClientAsync();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IHideezService/Ping", ReplyAction="http://tempuri.org/IHideezService/PingResponse")]
        [System.ServiceModel.FaultContractAttribute(typeof(ServiceLibrary.HideezServiceFault), Action="http://tempuri.org/IHideezService/PingHideezServiceFaultFault", Name="HideezServiceFault", Namespace="http://schemas.datacontract.org/2004/07/ServiceLibrary")]
        int Ping();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IHideezService/Ping", ReplyAction="http://tempuri.org/IHideezService/PingResponse")]
        System.Threading.Tasks.Task<int> PingAsync();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IHideezService/Shutdown", ReplyAction="http://tempuri.org/IHideezService/ShutdownResponse")]
        [System.ServiceModel.FaultContractAttribute(typeof(ServiceLibrary.HideezServiceFault), Action="http://tempuri.org/IHideezService/ShutdownHideezServiceFaultFault", Name="HideezServiceFault", Namespace="http://schemas.datacontract.org/2004/07/ServiceLibrary")]
        void Shutdown();
        
        [System.ServiceModel.OperationContractAttribute(Action="http://tempuri.org/IHideezService/Shutdown", ReplyAction="http://tempuri.org/IHideezService/ShutdownResponse")]
        System.Threading.Tasks.Task ShutdownAsync();
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IHideezServiceCallback {
        
        [System.ServiceModel.OperationContractAttribute(IsOneWay=true, Action="http://tempuri.org/IHideezService/LockWorkstationRequest")]
        void LockWorkstationRequest();
    }
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public interface IHideezServiceChannel : TestConsole.HideezServiceReference.IHideezService, System.ServiceModel.IClientChannel {
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.ServiceModel", "4.0.0.0")]
    public partial class HideezServiceClient : System.ServiceModel.DuplexClientBase<TestConsole.HideezServiceReference.IHideezService>, TestConsole.HideezServiceReference.IHideezService {
        
        public HideezServiceClient(System.ServiceModel.InstanceContext callbackInstance) : 
                base(callbackInstance) {
        }
        
        public HideezServiceClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName) : 
                base(callbackInstance, endpointConfigurationName) {
        }
        
        public HideezServiceClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName, string remoteAddress) : 
                base(callbackInstance, endpointConfigurationName, remoteAddress) {
        }
        
        public HideezServiceClient(System.ServiceModel.InstanceContext callbackInstance, string endpointConfigurationName, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(callbackInstance, endpointConfigurationName, remoteAddress) {
        }
        
        public HideezServiceClient(System.ServiceModel.InstanceContext callbackInstance, System.ServiceModel.Channels.Binding binding, System.ServiceModel.EndpointAddress remoteAddress) : 
                base(callbackInstance, binding, remoteAddress) {
        }
        
        public bool AttachClient(ServiceLibrary.ServiceClientParameters parameters) {
            return base.Channel.AttachClient(parameters);
        }
        
        public System.Threading.Tasks.Task<bool> AttachClientAsync(ServiceLibrary.ServiceClientParameters parameters) {
            return base.Channel.AttachClientAsync(parameters);
        }
        
        public void DetachClient() {
            base.Channel.DetachClient();
        }
        
        public System.Threading.Tasks.Task DetachClientAsync() {
            return base.Channel.DetachClientAsync();
        }
        
        public int Ping() {
            return base.Channel.Ping();
        }
        
        public System.Threading.Tasks.Task<int> PingAsync() {
            return base.Channel.PingAsync();
        }
        
        public void Shutdown() {
            base.Channel.Shutdown();
        }
        
        public System.Threading.Tasks.Task ShutdownAsync() {
            return base.Channel.ShutdownAsync();
        }
    }
}