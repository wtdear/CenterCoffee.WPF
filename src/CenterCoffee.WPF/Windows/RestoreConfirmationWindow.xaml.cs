using System.Windows;
using CenterCoffee.WPF.Mvvm;

namespace CenterCoffee.WPF.Windows
{
    public partial class RestoreConfirmationWindow : Window
    {
        private DialogViewModel _viewModel;

        public RestoreConfirmationWindow()
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
