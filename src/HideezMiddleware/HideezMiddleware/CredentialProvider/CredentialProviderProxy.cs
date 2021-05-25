using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.NamedPipes;
using Hideez.SDK.Communication.Utils;
using HideezMiddleware.ConnectionModeProvider;
using HideezMiddleware.Localize;

namespace HideezMiddleware.CredentialProvider
{
    // Commands to the Credential Provider
    public enum CredentialProviderCommandCode
    {
        Unknown = 0,
        Logon = 1,
        Error = 2,
        UserList = 3,
        PasswordChange = 4,
        PasswordChangeCompleated = 5,
        Notification = 6,
        Status = 7,
        GetPin = 8,
        HidePinUi = 9,
        GetConfirmedPin = 10,
        GetOldAndConfirmedPin = 11,
        GetActivationCode = 12,
        HideActivationCodeUi = 13,
        UpdateCommandLink = 14,
        GetPassword = 15,
        HidePasswordUi = 16,
    }

    // Events from the Credential Provider
    public enum CredentialProviderEventCode
    {
        Unknown = 0,
        LogonResolution = 101,
        UserListRequest = 102,
        BeginPasswordChangeRequest = 103,
        EndPasswordChangeRequest = 104,
        CheckPin = 105,
        LogonResult = 106,
        CheckActivationCode = 107,
        CheckPassword= 108,
        CommandLinkPressed = 109
    }

    public class CredentialProviderProxy : Logger, IWorkstationUnlocker, IClientUiProxy
    {
        readonly IConnectionModeProvider _connectionModeProvider;
        readonly PipeServer _pipeServer;

        public event EventHandler<EventArgs> Connected;

        public event EventHandler<EventArgs> ClientConnected;
        public event EventHandler<PinReceivedEventArgs> PinReceived;

        public event EventHandler<EventArgs> PinCancelled { add { } remove { } }

        public event EventHandler<ActivationCodeEventArgs> ActivationCodeReceived; // Todo:

        public event EventHandler<ActivationCodeEventArgs> ActivationCodeCancelled { add { } remove { } }

        public event EventHandler<EventArgs> ProviderActivated; // 101

        public event EventHandler<EventArgs> CommandLinkPressed; // 109

        public event EventHandler<PasswordEventArgs> PasswordReceived; 

        public bool IsConnected => _pipeServer.IsConnected;

        readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingLogonRequests
            = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        public CredentialProviderProxy(IConnectionModeProvider connectionModeProvider, ILog log)
            : base(nameof(CredentialProviderProxy), log)
        {
            _connectionModeProvider = connectionModeProvider;

            _pipeServer = new PipeServer("hideezsafe3", log);
            _pipeServer.MessageReceivedEvent += PipeServer_MessageReceivedEvent;
            _pipeServer.ClientConnectedEvent += PipeServer_ClientConnectedEvent;
        }

        public void Start()
        {
            _pipeServer.Start();
        }

        public void Stop()
        {
            _pipeServer.Stop();
        }

        void PipeServer_ClientConnectedEvent(object sender, ClientConnectedEventArgs e)
        {
            SafeInvoke(ClientConnected, EventArgs.Empty);
            SafeInvoke(Connected, EventArgs.Empty);
        }

        void PipeServer_MessageReceivedEvent(object sender, MessageReceivedEventArgs e)
        {
            WriteDebugLine($"PipeServer_MessageReceivedEvent {e.Buffer.Length} bytes length");
            try
            {
                byte[] buf = e.Buffer;
                int readBytes = e.ReadBytes;

                var code = (CredentialProviderEventCode)BitConverter.ToInt32(buf, 0);
                if (code == CredentialProviderEventCode.LogonResolution)
                {
                    var strings = ParseParams(buf, readBytes, expectedParamCount: 1);
                    OnLogonRequestByLoginName(strings[0]);
                }
                else if (code == CredentialProviderEventCode.BeginPasswordChangeRequest)
                {
                    var strings = ParseParams(buf, readBytes, expectedParamCount: 1);
                    OnPasswordChangeBegin(strings[0]);
                }
                else if (code == CredentialProviderEventCode.EndPasswordChangeRequest)
                {
                    var strings = ParseParams(buf, readBytes, expectedParamCount: 3);
                    OnPasswordChangeEnd(strings[0], strings[1], strings[2]);
                }
                else if (code == CredentialProviderEventCode.LogonResult)
                {
                    var strings = ParseParams(buf, readBytes, expectedParamCount: 3);
                    OnLogonResult(Convert.ToInt32(strings[0]), strings[1], strings[2]);
                }
                else if (code == CredentialProviderEventCode.CheckPin)
                {
                    var strings = ParseParams(buf, readBytes, expectedParamCount: 2);
                    OnCheckPin(strings[0], strings[1]);
                }
                else if (code == CredentialProviderEventCode.CheckActivationCode)
                {
                    var strings = ParseParams(buf, readBytes, expectedParamCount: 2);
                    OnCheckActivationCode(strings[0], strings[1]);
                }
                else if (code == CredentialProviderEventCode.CheckPassword)
                {
                    var strings = ParseParams(buf, readBytes, expectedParamCount: 2);
                    OnCheckPassword(strings[0], strings[1]);
                }
                else if (code == CredentialProviderEventCode.CommandLinkPressed)
                {
                    var strings = ParseParams(buf, readBytes, expectedParamCount: 1);
                    OnCommandLinkPressed(strings[0]);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        #region CP Message handlers 
        void OnPasswordChangeEnd(string v1, string v2, string v3)
        {
            throw new NotImplementedException();
        }

        void OnPasswordChangeBegin(string v)
        {
            throw new NotImplementedException();
        }

        // ntstatus - NTSTATUS type from the credential provider (see wincred.h for details)
        // error - is a string representation of the ntstatus code
        void OnLogonResult(int ntstatus, string userName, string error)
        {
            WriteLine($"OnLogonResult: {userName}, {error}, {ntstatus}");

            //todo 
            foreach (var tcs in _pendingLogonRequests.Values)
                tcs.TrySetResult(ntstatus == 0);

            //if (_pendingLogonRequests.TryGetValue(userName.ToUpperInvariant(), out TaskCompletionSource<bool> tcs))
            //    tcs.TrySetResult(ntstatus == 0);
        }

        void OnLogonRequestByLoginName(string login)
        {
            WriteLine($"LogonWorkstationAsync: {login}");

            SafeInvoke(ProviderActivated, EventArgs.Empty);
        }

        void OnCheckPin(string deviceId, string pin)
        {
            WriteLine($"OnCheckPin: {deviceId}");

            SafeInvoke(PinReceived, new PinReceivedEventArgs()
            {
                DeviceId = deviceId,
                Pin = pin
            });
        }

        void OnCheckActivationCode(string deviceId, string code)
        {
            WriteLine($"OnCheckActivationCode: {deviceId}");

            SafeInvoke(ActivationCodeReceived, new ActivationCodeEventArgs()
            {
                DeviceId = deviceId,
                Code = Encoding.UTF8.GetBytes(code)
            });
        }

        void OnCheckPassword(string deviceId, string password)
        {
            WriteLine($"OnCheckPassword: {deviceId}");

            SafeInvoke(PasswordReceived, new PasswordEventArgs()
            {
                DeviceId = deviceId,
                Password = password
            });
        }

        void OnCommandLinkPressed(string login)
        {
            WriteLine($"CommandLinkPressed: {login}");
            SafeInvoke(CommandLinkPressed, EventArgs.Empty);
        }
        #endregion CP Message handlers 

        #region Commands to CP
        public async Task<bool> SendLogonRequest(string login, string password, string prevPassword)
        {
            login = NormalizeLogin(login);
            WriteDebugLine($"SendLogonRequest: {login}");

            await SendMessageAsync(CredentialProviderCommandCode.Logon, true, $"{login}\n{password}\n{prevPassword}");

            var tcs = _pendingLogonRequests.GetOrAdd(login, (x) =>
            {
                return new TaskCompletionSource<bool>();
            });

            try
            {
                return await tcs.Task.TimeoutAfter(SdkConfig.CredentialProviderLogonTimeout);
            }
            catch (TimeoutException)
            {
                return false;
            }
            finally
            {
                _pendingLogonRequests.TryRemove(login, out TaskCompletionSource<bool> removed);
            }
        }

        string NormalizeLogin(string login)
        {
            if (login.StartsWith(".\\") || login.StartsWith("./"))
            {
                login = $"{Environment.MachineName}\\{login.Substring(2)}";
            }
            else if (login.StartsWith("@\\") || login.StartsWith("@/"))
            {
                login = $"MicrosoftAccount\\{login.Substring(2)}";
            }

            return login.ToUpperInvariant();
        }

        public async Task ShowPinUi(string deviceId, bool withConfirm, bool askOldPin)
        {
            WriteDebugLine($"SendGetPin: {deviceId}, withConfirm: {withConfirm}, askOldPin: {askOldPin}");

            var code = CredentialProviderCommandCode.GetPin;
            if (withConfirm)
                code = CredentialProviderCommandCode.GetConfirmedPin;
            else if (askOldPin)
                code = CredentialProviderCommandCode.GetOldAndConfirmedPin;

            await SendMessageAsync(code, true, $"{deviceId}");
        }

        public async Task ShowButtonConfirmUi(string deviceId)
        {
            // Ignore 
            // Handled by notifications
            await Task.CompletedTask;
        }

        public async Task HidePinUi()
        {
            WriteDebugLine($"HidePinUi");
            await SendMessageAsync(CredentialProviderCommandCode.HidePinUi, true, $"");
        }

        public async Task ShowActivationCodeUi(string deviceId)
        {
            WriteDebugLine($"SendGetActivationCode: {deviceId}");
            var code = CredentialProviderCommandCode.GetActivationCode;
            await SendMessageAsync(code, true, $"{deviceId}");
        }

        public async Task HideActivationCodeUi()
        {
            WriteDebugLine($"HideActivationCodeUi");
            await SendMessageAsync(CredentialProviderCommandCode.HideActivationCodeUi, true, $"");
        }

        public async Task SendError(string message, string notificationId = null)
        {
            WriteDebugLine($"SendError: {message}");

            string formattedMessage = "";
            if (!string.IsNullOrEmpty(message))
                formattedMessage = $"{DateTime.Now.ToLongTimeString()}: {message}";

            await SendMessageAsync(CredentialProviderCommandCode.Error, true, formattedMessage);
        }

        public async Task SendNotification(string message, string notificationId = null)
        {
            WriteDebugLine($"SendNotification: {message}");

            string formattedMessage = "";
            if (!string.IsNullOrEmpty(message))
                formattedMessage = $"{DateTime.Now.ToLongTimeString()}: {message}";

            await SendMessageAsync(CredentialProviderCommandCode.Notification, true, formattedMessage);
        }

        public async Task SendStatus(string statusMessage)
        {
            WriteDebugLine($"SendStatus: {statusMessage}");
            await SendMessageAsync(CredentialProviderCommandCode.Status, true, statusMessage);
        }

        public async Task ShowCommandLink(string commandLinkText = null)
        {
            WriteLine($"ShowCommandLink: {commandLinkText}");
            await SendMessageAsync(CredentialProviderCommandCode.UpdateCommandLink, true, $"true\n{commandLinkText ?? ""}");
        }

        public async Task HideCommandLink(string commandLinkText = null)
        {
            WriteLine($"HideCommandLink: {commandLinkText}");
            await SendMessageAsync(CredentialProviderCommandCode.UpdateCommandLink, true, $"false\n{commandLinkText ?? ""}");
        }

        public async Task ShowPasswordUi(string deviceId)
        {
            WriteDebugLine($"SendGetPassword: {deviceId}");
            var code = CredentialProviderCommandCode.GetPassword;
            await SendMessageAsync(code, true, $"{deviceId}");
        }

        public async Task HidePasswordUi()
        {
            WriteDebugLine($"HidePasswordUi");
            await SendMessageAsync(CredentialProviderCommandCode.HidePasswordUi, true, $"");
        }
        #endregion Commands to CP

        #region Utils
        async Task SendMessageAsync(CredentialProviderCommandCode code, bool isSuccess, string message)
        {
            WriteDebugLine($"SendMessageAsync {isSuccess}, {code}, {message}");

            try
            {
                if (_pipeServer != null && _pipeServer.IsConnected)
                {
                    byte[] byteMessage = Encoding.Unicode.GetBytes(message);

                    byte[] buf = new byte[6 + byteMessage.Length];

                    //version
                    buf[0] = 0x20;

                    // is success flag
                    buf[1] = (byte)(isSuccess ? 1 : 0);

                    // command code
                    byte[] bCode = BitConverter.GetBytes((int)code);
                    Buffer.BlockCopy(bCode, 0, buf, 2, bCode.Length);

                    // message
                    Buffer.BlockCopy(byteMessage, 0, buf, 6, byteMessage.Length);

                    await _pipeServer.WriteToAllAsync(buf);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        // bufLen can be less than the size of the buffer
        string[] ParseParams(byte[] buf, int bufLen, int expectedParamCount)
        {
            const int HEADER_LEN = 4;
            string prms = Encoding.Unicode.GetString(buf, HEADER_LEN, bufLen - HEADER_LEN);
            var strings = prms.Split('\n');
            if (strings.Length != expectedParamCount)
                throw new Exception();
            return strings;
        }
        #endregion Utils


        public async Task SendStatus(HesStatus hesStatus, RfidStatus rfidStatus, BluetoothStatus dongleStatus, BluetoothStatus bluetoothStatus, HesStatus tbHesStatus)
        {
            var statuses = new List<string>();

            if (dongleStatus != BluetoothStatus.Ok && _connectionModeProvider.IsCsrMode)
                statuses.Add(TranslationSource.Instance.Format("ServiceComponentStatus.Bluetooth.NotAvailable", dongleStatus));

            if (bluetoothStatus != BluetoothStatus.Ok && _connectionModeProvider.IsWinBleMode)
                statuses.Add(TranslationSource.Instance.Format("ServiceComponentStatus.Bluetooth.NotAvailable", bluetoothStatus));

            if (rfidStatus != RfidStatus.Disabled && rfidStatus != RfidStatus.Ok)
            {
                if (rfidStatus == RfidStatus.RfidServiceNotConnected)
                    statuses.Add(TranslationSource.Instance["ServiceComponentStatus.RFID.ServiceNotConnected"]);
                else if (rfidStatus == RfidStatus.RfidReaderNotConnected)
                    statuses.Add(TranslationSource.Instance["ServiceComponentStatus.RFID.ReaderNotConnected"]);
            }

            if (hesStatus != HesStatus.Disabled && hesStatus != HesStatus.Ok)
            {
                if (hesStatus == HesStatus.NotApproved)
                    statuses.Add(TranslationSource.Instance["ServiceComponentStatus.HES.WorkstationNotApproved"]);
                else
                    statuses.Add(TranslationSource.Instance["ServiceComponentStatus.HES.NotConnected"]);
            }

            if (statuses.Count > 0)
                await SendStatus(TranslationSource.Instance.Format("ServiceComponentStatus.Message.Base", string.Join("; ", statuses)));
            else
                await SendStatus(string.Empty);
        }

    }
}
