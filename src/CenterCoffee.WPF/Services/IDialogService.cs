using CenterCoffee.WPF.ViewModels.Dialogs;

namespace CenterCoffee.WPF.Services
{
    public interface IDialogService
    {
        bool ShowIngredientEditor(IngredientEditorViewModel viewModel);

        bool ShowStockOperation(StockOperationViewModel viewModel);

        bool ShowRestoreConfirmation(RestoreConfirmationViewModel viewModel);

        void ShowError(string message);

        void ShowInfo(string message);

        /// <summary>Выбор каталога для резервной копии. Возвращает null при отказе.</summary>
        string PickBackupDirectory(string initialDirectory);

        /// <summary>Выбор файла резервной копии для восстановления. Возвращает null при отказе.</summary>
        string PickBackupFile(string initialDirectory);
    }
}
