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

namespace HideezClient.Controls
{
    /// <summary>
    /// Interaction logic for AccountInfoControl.xaml
    /// </summary>
    public partial class AccountInfoControl : UserControl
    {
        public AccountInfoControl()
        {
            InitializeComponent();
        }

        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && (bool)textBlock.Tag)
            {
                string url = textBlock.Text;
                if (!string.IsNullOrWhiteSpace(url))
                    (DataContext as AccountInfoViewModel)?.OpenUrl("https://" + url);
            }
        }
    }
}
