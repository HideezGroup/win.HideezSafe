﻿using ServiceLibrary;
using ServiceLibrary.Implementation;
using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using TestConsole.HideezServiceReference;

namespace TestConsole
{
    class Program
    {
        private static ServiceHost serviceHost;

        static void Main(string[] args)
        {
            try
            {
                ThreadPool.QueueUserWorkItem(ConnectToHideezService);

                while (true)
                {
                    string line = Console.ReadLine();
                    if (line == "q" || line == "exit")
                    {
                        Console.WriteLine("exiting...");

                        ShutdownService().Wait();

                        if (serviceHost.State == CommunicationState.Faulted)
                        {
                            serviceHost.Abort();
                        }
                        else
                        {
                            serviceHost.Close();
                        }
                        break;
                    }
                }

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static async Task ShutdownService()
        {
            // ask service to finish all works and close all connections
            var callback = new HideezServiceCallbacks();
            var instanceContext = new InstanceContext(callback);

            var service = new HideezServiceClient(instanceContext);

            await service.ShutdownAsync();
        }

        private async static void ConnectToHideezService(Object param)
        {
            try
            {
                serviceHost = new ServiceHost(typeof(HideezService),
                                    new Uri("net.pipe://localhost/HideezService/"));

                // Enable debug information behavior
                ServiceDebugBehavior debug = serviceHost.Description.Behaviors.Find<ServiceDebugBehavior>();

                // if not found - add behavior with setting turned on 
                if (debug == null)
                {
                    serviceHost.Description.Behaviors.Add(new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });
                }
                else
                {
                    // make sure setting is turned ON
                    if (!debug.IncludeExceptionDetailInFaults)
                    {
                        debug.IncludeExceptionDetailInFaults = true;
                    }
                }

                serviceHost.Open();

                Console.WriteLine("running...");

                foreach (Uri uri in serviceHost.BaseAddresses)
                {
                    Console.WriteLine("Uri: {0}", uri.AbsoluteUri);
                }

                foreach (ServiceEndpoint endpoint in serviceHost.Description.Endpoints)
                {
                    Console.WriteLine("Address - {0}, binding: {1}, contract: {2}",
                        endpoint.Address,
                        endpoint.Binding.Name,
                        endpoint.Contract.Name);
                }

                // подключаемся к серверу, чтобы он стартовал
                var callback = new HideezServiceCallbacks();
                var instanceContext = new InstanceContext(callback);

                // NOTE: If an ambiguous reference error occurs, check that TestConsole DOES NOT have 
                // a reference to 'ServiceLibrary'. There should be only 'ServiceLibrary.Implementation' ref
                var service = new HideezServiceClient(instanceContext);
                await service.AttachClientAsync(new ServiceClientParameters() { ClientType = ClientType.TestConsole });

                // теперь можно отключиться
                service.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
