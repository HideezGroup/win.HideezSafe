using HideezClient.Modules;
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

namespace HideezClient.Controls.Notification.View
{
    /// <summary>
    /// Interaction logic for OpenClientFromTaskbarNotification.xaml
    /// </summary>
    public partial class OpenClientFromTaskbarNotification : NotificationBase
    {
        public OpenClientFromTaskbarNotification(NotificationOptions options)
            :base(options)
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            base.Close();
        }
    }
}
