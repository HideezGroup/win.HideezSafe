using Hideez.SDK.Communication;
using Hideez.SDK.Communication.Backup;
using Hideez.SDK.Communication.Log;
using Hideez.SDK.Communication.Security;
using HideezClient.Messages;
using HideezClient.Messages.Dialogs.BackupPassword;
using HideezClient.Modules;
using HideezMiddleware.Localize;
using HideezClient.Modules.Log;
using HideezClient.Tasks;
using HideezMiddleware.ApplicationModeProvider;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;
using MvvmExtensions.Commands;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Serialization;
using HideezClient.Messages.Dialogs;
using HideezClient.Dialogs;

namespace HideezClient.ViewModels
{
    class BackupCredentialsControlViewModel : ReactiveObject
    {
        readonly IActiveDevice _activeDevice;
        readonly Logger _log = LogManager.GetCurrentClassLogger(nameof(BackupCredentialsControlViewModel));
        readonly IMetaPubSub _messenger;

        CancellationTokenSource _cts;

        public BackupCredentialsControlViewModel(
            IApplicationModeProvider applicationModeProvider,
            IActiveDevice activeDevice,
            IMetaPubSub messenger)
        {
            var mode = applicationModeProvider.GetApplicationMode();
            if (mode != ApplicationMode.Standalone)
                return;
            _activeDevice = activeDevice;
            _messenger = messenger;
            _messenger.Subscribe<ActiveDeviceChangedMessage>(OnActiveDeviceChanged);

            Device = activeDevice.Device != null ? new DeviceViewModel(activeDevice.Device) : null;
        }

        #region Properties
        [Reactive] public DeviceViewModel Device { get; set; }

        #endregion

        #region Commands
        public ICommand RestoreCredentialsCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(OnRestoreCredentials);
                    }
                };
            }
        }

        public ICommand BackupCredentialsCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = x =>
                    {
                        Task.Run(OnBackupCredentials);
                    }
                };
            }
        }
        #endregion

        private async Task OnBackupCredentials()
        {
            try
            {
                _cts = new CancellationTokenSource();

                _messenger.Subscribe<BackupPasswordCancelledMessage>(OnBackupPasswordCancelled, msg => msg.DeviceId == Device.Id);

                SaveFileDialog dlg = new SaveFileDialog
                {
                    DefaultExt = ".hvb",
                    Filter = "Hideez vault backups (.hvb)|*.hvb", // Filter files by extension
                    FileName = "CredentialsBackup"
                };

                // Show open file dialog box
                var result = dlg.ShowDialog();

                if (result == true)
                {
                    string filename = dlg.FileName;
                    var passwordResult = await GetBackupPassword(true, filename);

                    if (passwordResult != null)
                    {
                        var backupProc = new CredentialsBackupProcedure();

                        await backupProc.Run(_activeDevice.Device.Storage, filename, passwordResult.Password, _cts.Token);

                        await _messenger.Publish(new SetResultUIBackupPasswordMessage(true));
                    }
                }
            }
            catch(Exception ex)
            {
                await _messenger.Publish(new SetResultUIBackupPasswordMessage(false));
                _log.WriteLine(ex);
            }
            finally
            {
                await _messenger.Unsubscribe<BackupPasswordCancelledMessage>(OnBackupPasswordCancelled);
                _cts.Dispose();
                _cts = null;
            }
        }

        private async Task OnRestoreCredentials()
        {
            try
            {
                OpenFileDialog dlg = new OpenFileDialog
                {
                    DefaultExt = ".hvb",
                    Filter = "Hideez vault backup (.hvb,*.hb)|*.hvb;*.hb|All files (*.*)|*.*" // Filter files by extension
                };

            // Show open file dialog box
            var result = dlg.ShowDialog();

            if (result == true)
            {
                ShowInfo(TranslationSource.Instance["Vault.Notification.EnterCurrentBackupPassword"]);

                while (true)
                {
                    try
                    {
                        string filename = dlg.FileName;
                        var passwordResult = await GetBackupPassword(false, filename);

                        if (passwordResult != null)
                        {
                            var restoreProc = new CredentialsRestoreProcedure();

                            restoreProc.ProgressChanged += RestoreProc_ProgressChanged;

                            await restoreProc.Run(_activeDevice.Device.Storage, filename, passwordResult.Password);

                            await _messenger.Publish(new SetResultUIBackupPasswordMessage(true));
                        }

                        break;
                    }
                    catch (OperationCanceledException ex)
                    {
                        _log.WriteLine(ex);
                        await _messenger.Publish(new SetResultUIBackupPasswordMessage(false));
                        break;
                    }
                    catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.FileCorruptOrPasswordIncorrect)
                    {
                        _log.WriteLine(ex);
                        await _messenger.Publish(new ShowErrorNotificationMessage(TranslationSource.Instance["BackupPassword.Error.WrongPassword"], notificationId: Device.Id));
                        await _messenger.Publish(new HideDialogMessage(typeof(BackupPasswordDialog)));
                    }
                    catch (NotSupportedException)
                    {
                        await _messenger.Publish(new SetResultUIBackupPasswordMessage(false, TranslationSource.Instance["BackupPassword.Error.NotSupportedFile"]));
                    }
                    catch (CryptographicException ex)
                    {
                        _log.WriteLine(ex);
                        await _messenger.Publish(new SetResultUIBackupPasswordMessage(false, TranslationSource.Instance["BackupPassword.Error.WrongPassword"]));
                    }
                    catch (Exception ex)
                    {
                        _log.WriteLine(ex);
                        await _messenger.Publish(new SetResultUIBackupPasswordMessage(false));
                    }
                }
            }
        }

        private void RestoreProc_ProgressChanged(object sender, RestoreProcedureStages e)
        {
            Task.Run(async () =>
            {
                switch (e)
                {
                    case RestoreProcedureStages.FileReading:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.FileReading"]));
                        break;
                    case RestoreProcedureStages.StorageErasing:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.StorageErasing"]));
                        break;
                    case RestoreProcedureStages.StartingWriting:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.StartingWriting"]));
                        break;
                    case RestoreProcedureStages.FirstTableWriting:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.FirstTableWriting"]));
                        break;
                    case RestoreProcedureStages.SecondTableWriting:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.SecondTableWriting"]));
                        break;
                    case RestoreProcedureStages.ThirdTableWriting:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.ThirdTableWriting"]));
                        break;
                    case RestoreProcedureStages.FourthTableWriting:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.FourthTableWriting"]));
                        break;
                    case RestoreProcedureStages.FifthTableWriting:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.FifthTableWriting"]));
                        break;
                    case RestoreProcedureStages.SixthTableWriting:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.SixthTableWriting"]));
                        break;
                    case RestoreProcedureStages.SeventhTableWriting:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.SeventhTableWriting"]));
                        break;
                    case RestoreProcedureStages.Finished:
                        await _messenger.Publish(new SetProgressUIBackupPasswordMessage(TranslationSource.Instance["RestoreProcedureStages.SeventhTableWriting"]));
                        break;
                }
            });
        }

        Task OnActiveDeviceChanged(ActiveDeviceChangedMessage obj)
        {
            Device = obj.NewDevice != null ? new DeviceViewModel(obj.NewDevice) : null;

            return Task.CompletedTask;
        }

        async Task<GetBackupPasswordProcResult> GetBackupPassword(bool isNewPassword, string fileName)
        {
            if(isNewPassword)
                ShowInfo(TranslationSource.Instance["Vault.Notification.NewBackupPassword"]);
            
            bool passwordOk = false;
            while (!passwordOk)
            {
                var bpProc = new GetBackupPasswordProc(_messenger, Device.Id, fileName, isNewPassword);
                var procResult = await bpProc.Run(SdkConfig.MainWorkflowTimeout);

                if (procResult == null)
                    return null;

                if (procResult.Password.Length == 0)
                {
                    // we received an empty PIN from the user. Trying again with the same timeout.
                    Debug.WriteLine($">>>>>>>>>>>>>>> EMPTY Backup Password");
                    _log.WriteLine("Received empty Backup Password");
                    continue;
                }
                return procResult;
            }
            return null;
        }

        void ShowInfo(string message)
        {
            _messenger?.Publish(new ShowInfoNotificationMessage(message,notificationId: Device.Id));
        }

        private Task OnBackupPasswordCancelled(BackupPasswordCancelledMessage msg)
        {
            _cts?.Cancel();

            return Task.CompletedTask;
        }
    }
}
