using HideezClient.ViewModels;
using MahApps.Metro.Controls;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using HideezClient.Mvvm;
using System.ComponentModel;

namespace HideezClient.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindowView : MetroWindow
    {
        private WeakPropertyObserver bindingRaiseEventSelectedDevice;
        private readonly List<WeakPropertyObserver> bindings = new List<WeakPropertyObserver>();

        public MainWindowView()
        {
            // This is required to avoid issue where certain buttons are shown as inactive until user performs some input action
            // Calling InvalidateRequerySuggested performs recalculation of all dependencies immediatelly instead of waiting for input
            DataContextChanged += DeviceInfo_DataContextChanged;
            InitializeComponent();
        }

        private void DeviceInfo_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Subscribe to active device change
            bindingRaiseEventSelectedDevice = new WeakPropertyObserver(e.NewValue, nameof(MainViewModel.ActiveDevice));
            bindingRaiseEventSelectedDevice.ValueChanged += (name, device) =>
            {
                this.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);

                bindings.Clear();

                if (device != null)
                {
                    // To subscribe to all properties, uncomment next line and remote the rest of weak property bindings
                    // bindings.Add(new WeakPropertyObserver(device, string.Empty));
                    bindings.Add(new WeakPropertyObserver(device, nameof(DeviceViewModel.IsConnected)));
                    bindings.Add(new WeakPropertyObserver(device, nameof(DeviceViewModel.FinishedMainFlow)));
                    bindings.Add(new WeakPropertyObserver(device, nameof(DeviceViewModel.IsAuthorized)));
                    bindings.Add(new WeakPropertyObserver(device, nameof(DeviceViewModel.IsAuthorizingRemoteDevice)));
                    bindings.Add(new WeakPropertyObserver(device, nameof(DeviceViewModel.IsCreatingRemoteDevice)));

                    bindings.ForEach(b => b.ValueChanged += DeviceValueChanged);
                }

                UpdateSoftwareKeyMenuItemPosition(device);
            };
        }

        private void DeviceValueChanged(string propName, object value)
        {
            this.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
            e.Cancel = true;
        }

        void UpdateSoftwareKeyMenuItemPosition(object device)
        {
            // if no device is active, software key menu item should be displayed in the upper menu
            // If active device is selected, software key menu item should be displayed in the bottom menu
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (device == null && SoftwareKeyMenuItem.Parent == BottomMenuStackPanel)
                {
                    BottomMenuStackPanel.Children.Remove(SoftwareKeyMenuItem);
                    SecurityTypeStackPanel.Children.Add(SoftwareKeyMenuItem);
                }
                else if (SoftwareKeyMenuItem.Parent == SecurityTypeStackPanel)
                {
                    SecurityTypeStackPanel.Children.Remove(SoftwareKeyMenuItem);
                    BottomMenuStackPanel.Children.Insert(1, SoftwareKeyMenuItem);
                }
            });
        }

        private void ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            // Prevents window from shaking when performing finger-drag on tablet
            e.Handled = true;
        }
    }
}
