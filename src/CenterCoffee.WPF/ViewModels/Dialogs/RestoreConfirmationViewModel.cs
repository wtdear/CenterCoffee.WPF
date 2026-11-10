using System;
using System.Collections.Generic;
using System.Windows.Input;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.WPF.Mvvm;

namespace CenterCoffee.WPF.ViewModels.Dialogs
{
    /// <summary>
    /// Подтверждение восстановления. Восстановление заменяет рабочие данные, поэтому
    /// пользователь должен увидеть, что именно будет восстановлено, до нажатия кнопки.
    /// </summary>
    public sealed class RestoreConfirmationViewModel : DialogViewModel
    {
        public RestoreConfirmationViewModel(string fileName, string filePath, DataIntegrityReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            FilePath = filePath ?? string.Empty;
            FileName = string.IsNullOrWhiteSpace(fileName) ? "резервная копия" : fileName;

            Title = "Восстановить данные";
            Subtitle = "Текущие данные будут заменены содержимым выбранной резервной копии.";

            SchemaVersionText = report.SchemaVersion.HasValue
                ? "версия " + report.SchemaVersion.Value
                : "не указана";
            CreatedAtText = report.LastWriteTime.HasValue
                ? report.LastWriteTime.Value.ToString("d MMMM yyyy, HH:mm")
                : "неизвестно";
            ContentsText = "позиций: " + report.IngredientsCount + ", операций: " + report.StockMovementsCount;
            HasWarnings = report.HasWarnings;

            var issues = new List<string>();
            foreach (var issue in report.Issues)
            {
                issues.Add("• " + issue.Details);
            }

            WarningText = string.Join(Environment.NewLine, issues.ToArray());

            ConfirmCommand = new RelayCommand(() => Close(true));
            CancelCommand = new RelayCommand(() => Close(false));
        }

        public string FileName { get; private set; }

        public string FilePath { get; private set; }

        public string SchemaVersionText { get; private set; }

        public string CreatedAtText { get; private set; }

        public string ContentsText { get; private set; }

        public bool HasWarnings { get; private set; }

        public string WarningText { get; private set; }

        public ICommand ConfirmCommand { get; private set; }

        public ICommand CancelCommand { get; private set; }
    }
}
