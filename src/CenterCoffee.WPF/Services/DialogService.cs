using System;
using System.IO;
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

        public bool ShowRestoreConfirmation(RestoreConfirmationViewModel viewModel)
        {
            var window = new RestoreConfirmationWindow
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

        public void ShowInfo(string message)
        {
            MessageBox.Show(
                GetOwner(),
                message ?? string.Empty,
                "Центр кофе",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public string PickBackupDirectory(string initialDirectory)
        {
            // Собственный диалог вместо System.Windows.Forms.FolderBrowserDialog:
            // проект не подключает WinForms только ради выбора папки.
            var picker = new BackupFolderPicker(initialDirectory);
            picker.Owner = GetOwner();
            return picker.ShowDialog() == true ? picker.SelectedDirectory : null;
        }

        public string PickBackupFile(string initialDirectory)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите файл резервной копии",
                Filter = "Резервная копия Центр кофе (*.json)|*.json|Все файлы (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = ResolveInitialDirectory(initialDirectory)
            };

            return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
        }

        private bool ShowDialog(Window window)
        {
            window.Owner = GetOwner();
            return window.ShowDialog() == true;
        }

        private static string ResolveInitialDirectory(string initialDirectory)
        {
            if (string.IsNullOrWhiteSpace(initialDirectory) || !Directory.Exists(initialDirectory))
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            return initialDirectory;
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
