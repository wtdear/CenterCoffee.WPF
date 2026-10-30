using System.Windows;
using CenterCoffee.WPF.Mvvm;

namespace CenterCoffee.WPF.Windows
{
    public partial class StockOperationWindow : Window
    {
        private DialogViewModel _viewModel;

        public StockOperationWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.CloseRequested -= OnCloseRequested;
            }

            _viewModel = e.NewValue as DialogViewModel;

            if (_viewModel != null)
            {
                _viewModel.CloseRequested += OnCloseRequested;
            }
        }

        private void OnCloseRequested(bool result)
        {
            DialogResult = result;
        }
    }
}
