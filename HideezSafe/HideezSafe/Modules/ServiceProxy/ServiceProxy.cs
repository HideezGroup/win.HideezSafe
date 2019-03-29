﻿using System;
using System.ServiceModel;
using System.Threading.Tasks;
using HideezSafe.HideezServiceReference;
using NLog;

namespace HideezSafe.Modules.ServiceProxy
{
    class ServiceProxy : IServiceProxy, IDisposable
    {
        private readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly IHideezServiceCallback callback;

        private HideezServiceClient service;

        public event EventHandler Connected;
        public event EventHandler Disconnected;

        public ServiceProxy(IHideezServiceCallback callback)
        {
            this.callback = callback;
        }

        public bool IsConnected
        {
            get
            {
                if (service == null)
                    return false;
                else
                    return service.State != CommunicationState.Faulted &&
                        service.State != CommunicationState.Closed;
            }
        }

        public IHideezService GetService()
        {
            if (!IsConnected)
                throw new ServiceNotConnectedException();

            return service;
        }

        public Task<bool> ConnectAsync()
        {
            return Task.Run(async () =>
            {
                if (service != null)
                    return true;

                var instanceContext = new InstanceContext(callback);
                service = new HideezServiceClient(instanceContext);

                SubscriveToServiceEvents(service);

                try
                {
                    var attached = await service.AttachClientAsync(new ServiceClientParameters()
                    {
                        ClientType = ClientType.DesktopClient
                    });

                    if (!attached)
                    {
                        UnsubscriveFromServiceEvents(service);
                        CloseServiceConnection(service);
                        service = null;
                    }

                    return attached;
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);

                    UnsubscriveFromServiceEvents(service);
                    CloseServiceConnection(service);
                    service = null;

                    return false;
                }
            });
        }

        public Task DisconnectAsync()
        {
            return Task.Run(() =>
            {
                if (service != null)
                {
                    if (service.State == CommunicationState.Opened)
                        service.DetachClient();

                    CloseServiceConnection(service);
                    UnsubscriveFromServiceEvents(service);
                    service = null;
                }
            });
        }

        private void CloseServiceConnection(HideezServiceClient service)
        {
            if (service == null)
                return;

            if (service.State != CommunicationState.Faulted)
                service.Close();
            else
                service.Abort();
        }

        private void SubscriveToServiceEvents(HideezServiceClient service)
        {
            if (service == null)
                return;

            var clientChannel = service.InnerDuplexChannel;
            clientChannel.Opened += Connected;
            clientChannel.Closed += Disconnected;
            clientChannel.Faulted += Disconnected;
        }

        private void UnsubscriveFromServiceEvents(HideezServiceClient service)
        {
            if (service == null)
                return;

            var clientChannel = service.InnerDuplexChannel;
            clientChannel.Opened -= Connected;
            clientChannel.Closed -= Disconnected;
            clientChannel.Faulted -= Disconnected;
        }

        #region IDisposable
        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                CloseServiceConnection(service);
                UnsubscriveFromServiceEvents(service);
                service = null;
            }

            disposed = true;
        }
        #endregion
    }
}
