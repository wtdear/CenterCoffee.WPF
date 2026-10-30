using CenterCoffee.WPF.ViewModels.Dialogs;

namespace CenterCoffee.WPF.Services
{
    public interface IDialogService
    {
        bool ShowIngredientEditor(IngredientEditorViewModel viewModel);

        bool ShowStockOperation(StockOperationViewModel viewModel);

        void ShowError(string message);
    }
}
