using HideezClient.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HideezClient.Controls
{
    /// <summary>
    /// Interaction logic for EditAccountControl.xaml
    /// </summary>
    public partial class EditAccountControl : UserControl
    {
        public EditAccountControl()
        {
            InitializeComponent();
            this.DataContextChanged += EditAccountControl_DataContextChanged;
            this.Loaded += EditAccountControl_Loaded;
        }

        private void EditAccountControl_Loaded(object sender, RoutedEventArgs e)
        {
            SetFcocus();
        }

        private void EditAccountControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.PasswordBox.Clear();
            SetFcocus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            PasswordBox.Clear();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null && sender is PasswordBox passwordBox)
            {
                ((dynamic)this.DataContext).IsPasswordChanged = passwordBox.SecurePassword.Length != 0;
            }
        }

        private void SetFcocus()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new Action(delegate ()
                {
                    try
                    {
                        PasswordBox.Focus();            // Set Logical Focus
                        Keyboard.Focus(PasswordBox);    // Set Keyboard Focus

                        AccountName.Focus();            // Set Logical Focus
                        Keyboard.Focus(AccountName);    // Set Keyboard Focus
                    }
                    catch (Exception) { }
                }));
        }

        private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Prevent selection of items when combobox is selected, dropdown is closed and mouse wheel is rolled up-down
            if (sender is ComboBox c)
            {
                if (!c.IsDropDownOpen)
                    e.Handled = true;
            }
        }

        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && (bool)textBlock.Tag)
            {
                string url = textBlock.Text;
                if (!string.IsNullOrWhiteSpace(url))
                    (DataContext as EditAccountViewModel)?.OpenUrl("https://" + url);
            }
        }
    }
}
