﻿using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.ServiceModel;
using ServiceLibrary.Implementation;
using HideezServiceHost.HideezServiceReference;

namespace HideezServiceHost
{
    public partial class Service1 : ServiceBase
    {
        ServiceHost serviceHost = null;

        public Service1()
        {
            InitializeComponent();
        }

        protected override async void OnStart(string[] args)
        {
            try
            {
                serviceHost = new ServiceHost(typeof(HideezService), new Uri("net.pipe://localhost/HideezService/"))
                {
                    CloseTimeout = new TimeSpan(0, 0, 1),
                };

                serviceHost.Open();

                // подключаемся к серверу, чтобы он стартовал
                var callback = new HideezServiceCallbacks();
                var instanceContext = new InstanceContext(callback);

                var service = new HideezServiceClient(instanceContext);
                await service.AttachClientAsync(new ServiceClientParameters() { ClientType = ClientType.TestConsole });

                // теперь можно отключиться
                service.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw;
            }
        }

        protected override void OnStop()
        {
            try
            {
                // connect and ask the service to finish all works and close all connections
                var callback = new HideezServiceCallbacks();
                var instanceContext = new InstanceContext(callback);

                var service = new HideezServiceClient(instanceContext);
                service.ShutdownAsync();

                // close the host
                if (serviceHost.State == CommunicationState.Faulted)
                {
                    serviceHost.Abort();
                }
                else
                {
                    serviceHost.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
