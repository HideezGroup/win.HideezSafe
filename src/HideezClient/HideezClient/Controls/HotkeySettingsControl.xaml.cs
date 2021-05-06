using HideezClient.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HideezClient.Controls
{
    /// <summary>
    /// Interaction logic for HotkeySettingsControl.xaml
    /// </summary>
    public partial class HotkeySettingsControl : UserControl
    {
        public event EventHandler<MouseWheelEventArgs> MouseWheelOverListView;

        public HotkeySettingsControl()
        {
            InitializeComponent();
            Loaded += HotkeySettingsControl_Loaded;
        }

        async void HotkeySettingsControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is HotkeySettingsViewModel viewModel)
                await viewModel.ReloadHotkeys();
        }

        async void HotKeyControl_GotFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is HotkeySettingsViewModel viewModel)
                await viewModel.ChangeHotkeyManagerState(false);
        }

        async void HotKeyControl_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is HotkeySettingsViewModel viewModel)
                await viewModel.ChangeHotkeyManagerState(true);
        }

        async void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (DataContext is HotkeySettingsViewModel viewModel)
                await viewModel.ChangeHotkeyManagerState(false);
        }

        async void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (DataContext is HotkeySettingsViewModel viewModel)
                await viewModel.ChangeHotkeyManagerState(true);
        }

        void ListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if(!IsComboBoxDropDownOpened())
                MouseWheelOverListView?.Invoke(this, e);
        }

        List<Control> AllChildren(DependencyObject parent)
        {
            var list = new List<Control>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Control control)
                    list.Add(control);
                list.AddRange(AllChildren(child));
            }
            return list;
        }

        bool IsComboBoxDropDownOpened()
        {
            foreach(var listViewItem in listView.Items)
            {
                var container = listView.ItemContainerGenerator.ContainerFromItem(listViewItem);
                var children = AllChildren(container);
                var comboBoxes = children.Where(c => c is ComboBox).Select(c=>c as ComboBox).ToList();
                foreach(var comboBox in comboBoxes)
                {
                    if (comboBox.IsDropDownOpen)
                        return true;
                }
            }

            return false;
        }
    }
}
