using System;
using System.Linq;
using System.Windows;
using CenterCoffee.WPF.ViewModels.Dialogs;
using CenterCoffee.WPF.Windows;

namespace CenterCoffee.WPF.Services
{
    public sealed class DialogService : IDialogService
    {
        public bool ShowIngredientEditor(IngredientEditorViewModel viewModel)
        {
            var window = new IngredientEditorWindow
            {
                DataContext = viewModel
            };

            return ShowDialog(window);
        }

        public bool ShowStockOperation(StockOperationViewModel viewModel)
        {
            var window = new StockOperationWindow
            {
                DataContext = viewModel
            };

            return ShowDialog(window);
        }

        public void ShowError(string message)
        {
            MessageBox.Show(
                GetOwner(),
                message ?? "Произошла неизвестная ошибка.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private bool ShowDialog(Window window)
        {
            window.Owner = GetOwner();
            return window.ShowDialog() == true;
        }

        private static Window GetOwner()
        {
            if (System.Windows.Application.Current == null)
            {
                return null;
            }

            var active = System.Windows.Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(candidate => candidate.IsActive);

            return active ?? System.Windows.Application.Current.MainWindow;
        }
    }
}
