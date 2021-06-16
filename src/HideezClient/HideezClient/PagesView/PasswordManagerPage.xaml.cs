using HideezClient.Mvvm;
using HideezClient.PageViewModels;
using HideezClient.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HideezClient.PagesView
{
    /// <summary>
    /// Interaction logic for PasswordManagerPage.xaml
    /// </summary>
    public partial class PasswordManagerPage : Page
    {
        private Storyboard AnimationHideAccountInfo;
        private Storyboard AnimationShowAccountInfo;

        public PasswordManagerPage()
        {
            InitializeComponent();
            AccountsList.Loaded += AccountsList_Loaded;
        }

        private void AccountsList_Loaded(object sender, RoutedEventArgs e)
        {
            AnimationHideAccountInfo = this.FindResource("AnimationHideAccountInfo") as Storyboard;
            AnimationShowAccountInfo = this.FindResource("AnimationShowAccountInfo") as Storyboard;

            UpdateAnimation();

            (DataContext as PasswordManagerViewModel)?.LoadedViewModelCommand.Execute(new object());
        }

        private void AccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAnimation();
        }

        private void UpdateAnimation()
        {
            if (AccountsList.SelectedItem == null)
            {
                AnimationShowAccountInfo?.Stop();
                AnimationHideAccountInfo?.Begin();
            }
            else
            {
                AnimationHideAccountInfo?.Stop();
                AnimationShowAccountInfo?.Begin();
            }
        }

        private void ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            // Prevents window from shaking when performing finger-drag on tablet
            e.Handled = true;
        }
    }
}
