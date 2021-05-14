using Hideez.SDK.Communication.Utils;
using HideezClient.Messages.Dialogs.BackupPassword;
using Meta.Lib.Modules.PubSub;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HideezClient.Tasks
{
    internal sealed class GetBackupPasswordProcResult
    {
        public string DeviceId { get; }

        public byte[] Password { get; }

        internal GetBackupPasswordProcResult(string deviceId, byte[] password)
        {
            DeviceId = deviceId;
            Password = password;
        }
    }

    internal sealed class GetBackupPasswordProc
    {
        readonly IMetaPubSub _messenger;
        readonly string _deviceId;
        readonly string _fileName;
        readonly bool _isNewPassword;
        readonly TaskCompletionSource<GetBackupPasswordProcResult> _tcs = new TaskCompletionSource<GetBackupPasswordProcResult>();

        public GetBackupPasswordProc(IMetaPubSub messenger, string deviceId, string fileName, bool isNewPassword = false)
        {
            _messenger = messenger;
            _deviceId = deviceId;
            _fileName = fileName;
            _isNewPassword = isNewPassword;
        }

        public async Task<GetBackupPasswordProcResult> Run(int timeout)
        {
            try
            {
                _messenger.Subscribe<SendBackupPasswordMessage>(OnBackupPasswordReceived, msg => msg.DeviceId == _deviceId);
                _messenger.Subscribe<BackupPasswordCancelledMessage>(OnBackupPasswordCancelled, msg => msg.DeviceId == _deviceId);
                _messenger.Subscribe<SetResultUIBackupPasswordMessage>(OnHideBackupPasswordUi);

                await _messenger.Publish(new ShowBackupPasswordUiMessage(_deviceId, _fileName, _isNewPassword));

                return await _tcs.Task.TimeoutAfter(timeout);
            }
            finally
            {
                await _messenger.Unsubscribe<SendBackupPasswordMessage>(OnBackupPasswordReceived);
                await _messenger.Unsubscribe<BackupPasswordCancelledMessage>(OnBackupPasswordCancelled);
                await _messenger.Unsubscribe<SetResultUIBackupPasswordMessage>(OnHideBackupPasswordUi);
            }
        }

        private Task OnBackupPasswordReceived(SendBackupPasswordMessage msg)
        {
            var procResult = new GetBackupPasswordProcResult(msg.DeviceId, msg.Password);
            _tcs.TrySetResult(procResult);

            return Task.CompletedTask;
        }

        private Task OnBackupPasswordCancelled(BackupPasswordCancelledMessage arg)
        {
            _tcs.TrySetCanceled();

            return Task.CompletedTask;
        }

        private Task OnHideBackupPasswordUi(SetResultUIBackupPasswordMessage msg)
        {
            _tcs.TrySetCanceled();

            return Task.CompletedTask;
        }
    }
}
