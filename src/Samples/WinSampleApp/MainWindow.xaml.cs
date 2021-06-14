using ReactiveUI;
using System.Windows;
using WinSampleApp.ViewModel;

namespace WinSampleApp
{
    public partial class MainWindow : Window, IViewFor<MainWindowViewModel>
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty
            .Register(nameof(ViewModel), typeof(MainWindowViewModel), typeof(MainWindow));

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            await ViewModel.Close();
            Properties.Settings.Default.Save();
        }

        public MainWindowViewModel ViewModel
        {
            get => (MainWindowViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (MainWindowViewModel)value;
        }
    }
}
