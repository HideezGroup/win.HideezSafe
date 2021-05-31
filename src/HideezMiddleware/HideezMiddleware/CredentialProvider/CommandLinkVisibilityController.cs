using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection;
using HideezMiddleware.DeviceConnection.Workflow.ConnectionFlow;
using HideezMiddleware.Localize;
using HideezMiddleware.Threading;
using System;
using System.Linq;
using System.Threading.Tasks;
using WinBle;

namespace HideezMiddleware.CredentialProvider
{
    /// <summary>
    /// Controls visibility of command link in credential provider based on the state 
    /// of <see cref="AdvertisementIgnoreList"/> and <see cref="WinBleConnectionManager"/>
    /// </summary>
    public sealed class CommandLinkVisibilityController : Logger, IDisposable
    {
        readonly CredentialProviderProxy _cpProxy;
        readonly WinBleConnectionManager _winBleConnectionManager;
        readonly ConnectionFlowProcessorBase _connectionFlowProcessor;
        readonly SemaphoreQueue _visibilityUpdateSemaphore = new SemaphoreQueue(1, 1);

        public CommandLinkVisibilityController(CredentialProviderProxy cpProxy, WinBleConnectionManager connectionManager, ConnectionFlowProcessorBase connectionFlowProcessor, ILog log)
            : base(nameof(CommandLinkVisibilityController), log)
        {
            _cpProxy = cpProxy;
            _winBleConnectionManager = connectionManager;
            _connectionFlowProcessor = connectionFlowProcessor;

            _cpProxy.Connected += CredentialProviderProxy_Connected;
            _cpProxy.ProviderActivated += CredentialProviderProxy_Activated;

            _winBleConnectionManager.ControllerAdded += ConnectionManager_BondedControllerAdded;
            _winBleConnectionManager.ControllerRemoved += ConnectionManager_BondedControllerRemoved;
            
            _connectionFlowProcessor.Started += ConnectionFlowProcessor_Started;
            _connectionFlowProcessor.Finished += ConnectionFlowProcessor_Finished;
        }

        async void CredentialProviderProxy_Connected(object sender, EventArgs e)
        {
            await UpdateControlLinkVisibility();
        }

        async void CredentialProviderProxy_Activated(object sender, EventArgs e)
        {
            await UpdateControlLinkVisibility();
        }

        async void ConnectionManager_BondedControllerAdded(object sender, ControllerAddedEventArgs e)
        {
            await UpdateControlLinkVisibility();
        }

        async void ConnectionManager_BondedControllerRemoved(object sender, ControllerRemovedEventArgs e)
        {
            await UpdateControlLinkVisibility();
        }

        async void ConnectionFlowProcessor_Started(object sender, string e)
        {
            await UpdateControlLinkVisibility();
        }

        async void ConnectionFlowProcessor_Finished(object sender, string e)
        {
            await UpdateControlLinkVisibility();
        }

        /// <summary>
        /// <para>Changes control link visibility in credential provider.
        /// Control link will be displayed if all conditions are true:</para>
        /// <br>- At least one device is paired through WinBle</br>
        /// <br>- Workflow is not running</br>
        /// <para>Otherwise, control link will be hidden.</para>
        /// </summary>
        async Task UpdateControlLinkVisibility()
        {
            await _visibilityUpdateSemaphore.WaitAsync();
            try
            {
                // No reason sending anything if credential provider is not connected
                if (_cpProxy.IsConnected)
                {
                    if (_winBleConnectionManager.ConnectionControllers.Count == 0 || _connectionFlowProcessor.IsRunning || _winBleConnectionManager.State != BluetoothAdapterState.PoweredOn)
                    {
                        await _cpProxy.HideCommandLink();
                    }
                    else
                    {
                        string linkMessage = TranslationSource.Instance["CredentialProvider.CommandLink.Unlock.Generic"];
                        if (_winBleConnectionManager.ConnectionControllers.Count == 1)
                        {
                            var controller = _winBleConnectionManager.ConnectionControllers.FirstOrDefault();
                            linkMessage = string.Format(TranslationSource.Instance["CredentialProvider.CommandLink.Unlock.Specific"], controller?.Name ?? "Hideez Vault");
                        }

                        await _cpProxy.ShowCommandLink(linkMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
            finally
            {
                _visibilityUpdateSemaphore.Release();
            }
        }

        public void Dispose()
        {
            _cpProxy.Connected -= CredentialProviderProxy_Connected;

            _winBleConnectionManager.ControllerAdded -= ConnectionManager_BondedControllerAdded;
            _winBleConnectionManager.ControllerRemoved -= ConnectionManager_BondedControllerRemoved;

            _connectionFlowProcessor.Started -= ConnectionFlowProcessor_Started;
            _connectionFlowProcessor.Finished -= ConnectionFlowProcessor_Finished;
        }
    }
}
