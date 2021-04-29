using System;
using System.ComponentModel;
using System.Windows;
using DeviceMaintenance.Messages;
using DeviceMaintenance.ViewModel;
using MahApps.Metro.Controls;
using Meta.Lib.Modules.PubSub;

namespace DeviceMaintenance
{
    public partial class MainWindow : MetroWindow
    {
        private readonly MetaPubSub _hub;

        public MainWindow()
        {
            InitializeComponent();
            _hub = new MetaPubSub();
            DataContext = new MainWindowViewModel(_hub);
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            var vm = (MainWindowViewModel)DataContext;

            if (vm.IsFirmwareUpdateInProgress)
            {
                var mb = MessageBox.Show(
                    "Firmware update in progress!" +
                    Environment.NewLine +
                    "Are you sure you want to exit?",
                    "Exit application",
                    MessageBoxButton.YesNoCancel, 
                    MessageBoxImage.Exclamation);

                if (mb != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            try
            {
                await _hub.Publish(new ClosingEvent());
                base.OnClosing(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                e.Cancel = true;
            }
        }

        private void WrapPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if((bool)e.NewValue)
            {
                Width = 400;
                Height = 300;
            }
            else
            {
                Width = 1000;
                Height = 450;
            }
        }
    }
}
