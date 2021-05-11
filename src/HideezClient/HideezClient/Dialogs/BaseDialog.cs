using Hideez.SDK.Communication.Utils;
using HideezClient.ViewModels.Dialog;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace HideezClient.Dialogs
{
    public abstract class BaseDialog: BaseMetroDialog
    {
        public event EventHandler Closed;
        readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        protected BaseDialog(IDialogViewModel dialogViewModel)
        {
            DataContext = dialogViewModel;
        }

        public virtual async Task Close()
        {
            MetroWindow metroWindow = Application.Current.Dispatcher.Invoke(() =>  Application.Current.MainWindow as MetroWindow);
            if (metroWindow != null)
            {
                while (!_tcs.Task.IsCompleted)
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            await metroWindow.HideMetroDialogAsync(this);
                            _tcs.TrySetResult(true);
                        });

                        await _tcs.Task.TimeoutAfter(1000);
                        Closed?.Invoke(this, EventArgs.Empty);
                        Application.Current.Dispatcher.Invoke(() => (DataContext as IDialogViewModel).OnClose());
                    }
                    catch(Exception ex)
                    {
                        Debug.WriteLine($"{this.GetType()} closing exception: {ex.Message}");
                        continue;
                    }
                }
            }
        }
    }
}
