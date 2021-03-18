﻿using HideezClient.Mvvm;
using HideezMiddleware.IPC.IncommingMessages;
using Meta.Lib.Modules.PubSub;
using MvvmExtensions.Commands;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HideezClient.ViewModels
{
    class ReconnectPairedVaultsControlViewModel : LocalizedObject
    {
        readonly IMetaPubSub _messenger;

        bool _inProgress = false;

        public ReconnectPairedVaultsControlViewModel(IMetaPubSub messenger)
        {
            _messenger = messenger;
        }

        public bool InProgress
        {
            get { return _inProgress; }
            set { Set(ref _inProgress, value); }
        }

        public ICommand ReconnectPairedVaultsCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = async x =>
                    {
                        await ReconnectPairedVaultsProc();
                    }
                };
            }
        }

        async Task ReconnectPairedVaultsProc()
        {
            try
            {
                InProgress = true;
                await _messenger.PublishOnServer(new ConnectPairedVaultsMessage
                {
                    ResponseTimeout = 60_000,
                });
            }
            catch (Exception)
            {
            }
            finally
            {
                InProgress = false;
            }
        }
    }
}
