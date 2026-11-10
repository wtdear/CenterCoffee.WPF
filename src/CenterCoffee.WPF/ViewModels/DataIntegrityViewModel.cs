using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.WPF.Common;
using CenterCoffee.WPF.Mvvm;
using CenterCoffee.WPF.Services;
using CenterCoffee.WPF.ViewModels.Dialogs;

namespace CenterCoffee.WPF.ViewModels
{
    public sealed class DataIntegrityIssueRowViewModel
    {
        public DataIntegrityIssueRowViewModel(DataIntegrityIssue issue)
        {
            if (issue == null)
            {
                throw new ArgumentNullException(nameof(issue));
            }

            Title = issue.Title;
            Details = issue.Details;
            IsError = issue.IsError;
            SeverityText = issue.IsError ? "Ошибка" : "Замечание";
        }

        public string Title { get; private set; }

        public string Details { get; private set; }

        public bool IsError { get; private set; }

        public string SeverityText { get; private set; }
    }

    /// <summary>
    /// Экран целостности и резервного копирования. Показывает состояние локального файла,
    /// позволяет создать копию отдельным файлом и восстановить данные из копии.
    /// </summary>
    public sealed class DataIntegrityViewModel : ViewModelBase
    {
        private readonly IDataMaintenanceService _maintenance;
        private readonly IDialogService _dialogs;

        private bool _isBusy;
        private string _statusMessage;
        private bool _statusIsError;

        private DataIntegrityLevel _level = DataIntegrityLevel.FileMissing;
        private bool _hasReport;
        private string _filePathText = string.Empty;
        private string _schemaVersionText = string.Empty;
        private string _lastWriteText = "—";
        private string _contentsText = "—";
        private string _backupBlockReason;

        public DataIntegrityViewModel(IDataMaintenanceService maintenance, IDialogService dialogs)
        {
            _maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

            Issues = new ObservableCollection<DataIntegrityIssueRowViewModel>();

            RefreshCommand = new RelayCommand(Refresh, () => !IsBusy);
            CreateBackupCommand = new RelayCommand(CreateBackup, CanCreateBackup);
            RestoreCommand = new RelayCommand(Restore, () => !IsBusy && HasReport && Level != DataIntegrityLevel.Failed);

            Title = "Данные и резервные копии";
            Subtitle = "Целостность локальной базы и восстановление из отдельного файла";
        }

        public ObservableCollection<DataIntegrityIssueRowViewModel> Issues { get; private set; }

        public bool HasReport
        {
            get { return _hasReport; }
            private set { Set(ref _hasReport, value); }
        }

        public DataIntegrityLevel Level
        {
            get { return _level; }
            private set
            {
                if (Set(ref _level, value))
                {
                    OnPropertyChanged(nameof(LevelTitle));
                    OnPropertyChanged(nameof(LevelDescription));
                    OnPropertyChanged(nameof(HasIssues));
                    OnPropertyChanged(nameof(LevelBrush));
                    ReloadCommands();
                }
            }
        }

        /// <summary>Цвет индикатора состояния файла данных.</summary>
        public System.Windows.Media.Brush LevelBrush
        {
            get
            {
                switch (_level)
                {
                    case DataIntegrityLevel.Ok:
                        return StatusPalette.Success;
                    case DataIntegrityLevel.Warnings:
                    case DataIntegrityLevel.VersionUpgradePending:
                        return StatusPalette.Warning;
                    case DataIntegrityLevel.Failed:
                        return StatusPalette.Danger;
                    default:
                        return StatusPalette.Muted;
                }
            }
        }

        public string LevelTitle
        {
            get
            {
                switch (_level)
                {
                    case DataIntegrityLevel.Ok:
                        return "Данные в порядке";
                    case DataIntegrityLevel.Warnings:
                        return "Есть замечания";
                    case DataIntegrityLevel.VersionUpgradePending:
                        return "Старая версия формата";
                    case DataIntegrityLevel.Failed:
                        return "Файл данных недоступен";
                    default:
                        return "Файл данных ещё не создан";
                }
            }
        }

        public string LevelDescription
        {
            get
            {
                switch (_level)
                {
                    case DataIntegrityLevel.Ok:
                        return "Файл прочитан, версия формата поддерживается, расхождений не найдено.";
                    case DataIntegrityLevel.Warnings:
                        return "Файл читается, но найдены несогласованности. Проверьте список ниже перед продолжением работы.";
                    case DataIntegrityLevel.VersionUpgradePending:
                        return "Файл сохранён без явной версии формата. Он откроется, а при следующем сохранении будет приведён к текущей версии.";
                    case DataIntegrityLevel.Failed:
                        return "Файл повреждён или имеет несовместимую версию. Работы со складом недоступны, пока данные не исправлены или не восстановлены из резервной копии.";
                    default:
                        return "Это нормальное состояние новой установки. Файл появится после первого сохранения, например после добавления позиции.";
                }
            }
        }

        public bool HasIssues
        {
            get { return _level == DataIntegrityLevel.Warnings || _level == DataIntegrityLevel.Failed; }
        }

        public string FilePathText
        {
            get { return _filePathText; }
            private set { Set(ref _filePathText, value); }
        }

        public string SchemaVersionText
        {
            get { return _schemaVersionText; }
            private set { Set(ref _schemaVersionText, value); }
        }

        public string LastWriteText
        {
            get { return _lastWriteText; }
            private set { Set(ref _lastWriteText, value); }
        }

        public string ContentsText
        {
            get { return _contentsText; }
            private set { Set(ref _contentsText, value); }
        }

        public string BackupBlockReason
        {
            get { return _backupBlockReason; }
            private set
            {
                if (Set(ref _backupBlockReason, value))
                {
                    OnPropertyChanged(nameof(CanCreateBackupNow));
                    ReloadCommands();
                }
            }
        }

        public bool CanCreateBackupNow
        {
            get { return string.IsNullOrEmpty(_backupBlockReason); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                if (Set(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                    ReloadCommands();
                }
            }
        }

        public bool IsIdle
        {
            get { return !_isBusy; }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            private set
            {
                if (Set(ref _statusMessage, value))
                {
                    OnPropertyChanged(nameof(HasStatus));
                }
            }
        }

        public bool HasStatus
        {
            get { return !string.IsNullOrWhiteSpace(_statusMessage); }
        }

        /// <summary>Цвет баннера статуса: ошибка или успех.</summary>
        public bool StatusIsError
        {
            get { return _statusIsError; }
            private set { Set(ref _statusIsError, value); }
        }

        public ICommand RefreshCommand { get; private set; }

        public ICommand CreateBackupCommand { get; private set; }

        public ICommand RestoreCommand { get; private set; }

        public override void OnActivated()
        {
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                ApplyReport(_maintenance.InspectData());
                StatusMessage = null;
            }
            catch (Exception exception)
            {
                ApplyFailure("Не удалось проверить файл данных. " + exception.Message);
            }
        }

        private bool CanCreateBackup()
        {
            return !IsBusy && HasReport && CanCreateBackupNow;
        }

        private void CreateBackup()
        {
            var directory = _dialogs.PickBackupDirectory(GetDefaultBackupDirectory());
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            BackupOperationResult result;
            IsBusy = true;
            try
            {
                result = _maintenance.CreateBackup(directory);
            }
            catch (Exception exception)
            {
                IsBusy = false;
                ShowStatus("Не удалось создать резервную копию. " + exception.Message, true);
                return;
            }
            finally
            {
                IsBusy = false;
            }

            if (!result.Success)
            {
                ShowStatus(result.Message, true);
                return;
            }

            ShowStatus(result.Message + " Файл: " + result.BackupPath + ".", false);
            Refresh();
        }

        private void Restore()
        {
            var path = _dialogs.PickBackupFile(GetDefaultBackupDirectory());
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            DataIntegrityReport report;
            try
            {
                report = _maintenance.InspectBackup(path);
            }
            catch (Exception exception)
            {
                ShowStatus("Не удалось проверить выбранный файл. " + exception.Message, true);
                return;
            }

            if (report.Level == DataIntegrityLevel.Failed || report.Level == DataIntegrityLevel.FileMissing)
            {
                _dialogs.ShowError(
                    "Выбранный файл не является корректной резервной копией, восстановление отменено. " +
                    DescribeIssues(report));
                return;
            }

            var confirm = new Dialogs.RestoreConfirmationViewModel(Path.GetFileName(path), path, report);
            if (!_dialogs.ShowRestoreConfirmation(confirm))
            {
                return;
            }

            BackupOperationResult result;
            IsBusy = true;
            try
            {
                result = _maintenance.RestoreFromBackup(path);
            }
            catch (Exception exception)
            {
                IsBusy = false;
                ShowStatus("Восстановление не выполнено, текущие данные не изменены. " + exception.Message, true);
                return;
            }
            finally
            {
                IsBusy = false;
            }

            if (!result.Success)
            {
                // Файл мог быть уже заменён, но приложение не смогло перечитать данные.
                // Тогда работа со складом запрещается до перезапуска.
                if (result.RequiresRestart)
                {
                    DataChangedNotifier.RaiseDataBlocked();
                    _dialogs.ShowError(result.Message);
                    return;
                }

                ShowStatus(result.Message, true);
                return;
            }

            DataChangedNotifier.RaiseDataReplaced();
            ShowStatus(result.Message, false);
            Refresh();
        }

        private void ApplyReport(DataIntegrityReport report)
        {
            HasReport = true;
            Level = report.Level;

            FilePathText = report.FilePath;
            SchemaVersionText = report.SchemaVersion.HasValue
                ? "версия " + report.SchemaVersion.Value + " (поддерживается " + report.SupportedSchemaVersion + ")"
                : "не указана";
            LastWriteText = report.LastWriteTime.HasValue
                ? report.LastWriteTime.Value.ToString("d MMMM yyyy, HH:mm")
                : "—";
            ContentsText = report.FileExists
                ? "позиций: " + report.IngredientsCount + ", операций: " + report.StockMovementsCount
                : "нет данных";

            ReplaceIssues(report);

            switch (report.Level)
            {
                case DataIntegrityLevel.Failed:
                    BackupBlockReason = "Резервную копию нельзя создать: файл данных повреждён или несовместим.";
                    break;
                case DataIntegrityLevel.FileMissing:
                    BackupBlockReason = "Резервную копию можно создать после первого сохранения данных.";
                    break;
                default:
                    BackupBlockReason = null;
                    break;
            }
        }

        private void ReplaceIssues(DataIntegrityReport report)
        {
            Issues.Clear();
            foreach (var issue in report.Issues)
            {
                Issues.Add(new DataIntegrityIssueRowViewModel(issue));
            }
        }

        private void ApplyFailure(string message)
        {
            HasReport = true;
            Level = DataIntegrityLevel.Failed;
            FilePathText = _maintenance.DataFilePath;
            SchemaVersionText = "неизвестно";
            LastWriteText = "—";
            ContentsText = "не удалось прочитать";
            BackupBlockReason = "Проверьте доступ к файлу данных.";
            Issues.Clear();
            ShowStatus(message, true);
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusIsError = isError;
            StatusMessage = message;
        }

        private string GetDefaultBackupDirectory()
        {
            try
            {
                var directory = Path.GetDirectoryName(_maintenance.DataFilePath);
                return string.IsNullOrEmpty(directory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : directory;
            }
            catch (ArgumentException)
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        private void ReloadCommands()
        {
            ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CreateBackupCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RestoreCommand).RaiseCanExecuteChanged();
        }

        private static string DescribeIssues(DataIntegrityReport report)
        {
            if (report == null || !report.HasWarnings)
            {
                return string.Empty;
            }

            return string.Join(" ", report.Issues.Select(issue => issue.Details).ToArray());
        }
    }
}
