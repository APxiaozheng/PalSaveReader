using PalSearch.UI.ViewModel;
using System.Windows;

namespace PalSearch.UI
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainWindowViewModel();
            DataContext = viewModel;
            Loaded += async (s, e) => await viewModel.InitializeAsync();
        }
    }
}