﻿using HideezSafe.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HideezSafe.Views
{
    /// <summary>
    /// Interaction logic for NotificationsWindow.xaml
    /// </summary>
    public partial class NotificationsContainerWindow : Window
    {
        public NotificationsContainerWindow()
        {
            InitializeComponent();

            if (notifyItems.Items is INotifyCollectionChanged notifyCollectionChanged)
            {
                notifyCollectionChanged.CollectionChanged += NotificationsContainerWindow_CollectionChanged;
            }
        }

        private void NotificationsContainerWindow_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateWindowContainer();
        }

        private void UpdateWindowContainer()
        {
            UpdateLayout();
            Height = WorkAreaHeight;
            var point = GetPositionForBottomRightCorner(ActualWidth, ActualHeight);
            Left = point.X;
            Top = point.Y;
        }

        #region PrimaryScreen

        private double ScreenHeight => SystemParameters.PrimaryScreenHeight;
        private double ScreenWidth => SystemParameters.PrimaryScreenWidth;
        private double WorkAreaHeight => SystemParameters.WorkArea.Height;
        private double WorkAreaWidth => SystemParameters.WorkArea.Width;

        private (double X, double Y) GetPositionForBottomRightCorner(double actualWidth, double actualHeight)
        {
            double pointX = WorkAreaWidth - actualWidth;
            double pointY = WorkAreaHeight - actualHeight;

            if (SystemParameters.WorkArea.Left > 0)
            {
                pointX = ScreenWidth - actualWidth;
            }

            if (SystemParameters.WorkArea.Top > 0)
            {
                pointY = ScreenHeight - actualHeight;
            }

            return (pointX, pointY);
        }

        #endregion PrimaryScreen


        #region HideWindow
        // Hide a WPF form from Alt+Tab

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private const int GWL_EX_STYLE = -20;
        private const int WS_EX_APPWINDOW = 0x00040000, WS_EX_TOOLWINDOW = 0x00000080;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Variable to hold the handle for the form
            var helper = new WindowInteropHelper(this).Handle;
            //Performing some magic to hide the form from Alt+Tab
            SetWindowLong(helper, GWL_EX_STYLE, (GetWindowLong(helper, GWL_EX_STYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        }

        #endregion
    }
}
