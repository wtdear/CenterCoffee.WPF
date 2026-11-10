using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CenterCoffee.Application.Abstractions;

namespace CenterCoffee.Infrastructure.Persistence
{
    /// <summary>
    /// Проверка целостности и резервное копирование локального JSON-файла.
    /// Резервная копия — отдельный файл с тем же документом и расширением .json,
    /// чтобы её можно было перенести или открыть без приложения.
    /// </summary>
    public sealed class JsonDataMaintenanceService : IDataMaintenanceService
    {
        private const string BackupFileExtension = ".json";
        private const string SafetyCopySuffix = ".before-restore";
        private const int MaxDuplicateIndex = 100;

        private readonly JsonDataStore _store;
        private readonly Action _afterRestore;

        public JsonDataMaintenanceService(JsonDataStore store)
            : this(store, null)
        {
        }

        /// <param name="afterRestore">
        /// Необязательное действие после успешного восстановления — например, перечитать
        /// данные в текущем <see cref="IUnitOfWork"/>, чтобы в памяти не остались старые данные.
        /// </param>
        public JsonDataMaintenanceService(JsonDataStore store, Action afterRestore)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _afterRestore = afterRestore;
        }

        public string DataFilePath => _store.FilePath;

        public int SupportedSchemaVersion => DataSchema.Version;

        public DataIntegrityReport InspectData()
        {
            return BuildReport(_store.Inspect());
        }

        public DataIntegrityReport InspectBackup(string backupPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                throw new ArgumentException("Не указан путь к резервной копии.", nameof(backupPath));
            }

            return BuildReport(new JsonDataStore(backupPath).Inspect());
        }

        public BackupOperationResult CreateBackup(string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new ArgumentException("Не выбран каталог для резервной копии.", nameof(targetDirectory));
            }

            var inspection = _store.Inspect();

            if (inspection.State == DataFileState.Missing)
            {
                return Failure("Рабочий файл данных ещё не создан: копировать пока нечего.");
            }

            if (inspection.State == DataFileState.Corrupted || inspection.State == DataFileState.UnsupportedVersion)
            {
                return Failure(
                    "Резервную копию создать нельзя: рабочий файл повреждён или несовместим. " +
                    "Сначала проверьте данные в разделе «Целостность».");
            }

            string backupPath;
            try
            {
                backupPath = BuildUniqueBackupPath(targetDirectory);
                _store.SaveTo(backupPath, inspection.Data);
            }
            catch (UnauthorizedAccessException)
            {
                return Failure("Нет прав на запись в выбранный каталог. Выберите другую папку.");
            }
            catch (IOException exception)
            {
                return Failure("Не удалось сохранить резервную копию: " + exception.Message);
            }

            var saved = new JsonDataStore(backupPath).Inspect();
            if (saved.State != DataFileState.Loaded && saved.State != DataFileState.NeedsUpgrade)
            {
                TryDelete(backupPath);
                return Failure("Резервная копия записана, но не прошла проверку. Непроверенный файл удалён.");
            }

            return new BackupOperationResult(
                true,
                "Резервная копия создана и проверена: " + FormatCounts(inspection.Data) + ".",
                backupPath,
                GetLastWriteTime(backupPath),
                saved.SchemaVersion,
                BuildReport(saved).Issues);
        }

        public BackupOperationResult RestoreFromBackup(string backupPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                throw new ArgumentException("Не указан путь к резервной копии.", nameof(backupPath));
            }

            var source = new JsonDataStore(backupPath);
            var inspection = source.Inspect();

            if (inspection.State != DataFileState.Loaded && inspection.State != DataFileState.NeedsUpgrade)
            {
                return Failure("Восстановление невозможно: выбранный файл не является корректной резервной копией. " +
                               inspection.ErrorMessage);
            }

            var safetyCopyPath = BuildSafetyCopyPath(backupPath);

            try
            {
                _store.Replace(source, safetyCopyPath);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException)
            {
                return Failure("Восстановление не выполнено, текущие данные не изменены. " + exception.Message);
            }

            var restored = _store.Inspect();
            var report = BuildReport(restored);

            if (_afterRestore != null)
            {
                try
                {
                    _afterRestore();
                }
                catch (Exception exception)
                {
                    return new BackupOperationResult(
                        false,
                        true,
                        "Файл данных заменён восстановленной копией, но приложение не смогло перечитать его: " +
                        exception.Message + ". Работа со складом заблокирована, чтобы не перезаписать " +
                        "восстановленные данные. Закройте и снова откройте программу. " +
                        "Страховочная копия прежних данных: " + safetyCopyPath + ".",
                        backupPath,
                        GetLastWriteTime(backupPath),
                        restored.SchemaVersion,
                        report.Issues);
                }
            }

            return new BackupOperationResult(
                true,
                "Данные восстановлены из резервной копии: " + FormatCounts(restored.Data) + ". " +
                "Страховочная копия прежних данных: " + safetyCopyPath + ".",
                backupPath,
                GetLastWriteTime(backupPath),
                restored.SchemaVersion,
                report.Issues);
        }

        /// <summary>
        /// Имя файла копии: имя рабочего файла плюс дата создания, например
        /// <c>data-20261110-142530.json</c>.
        /// </summary>
        public static string BuildBackupFileName(string dataFileName, DateTime timestamp)
        {
            var name = (dataFileName ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                name = "data";
            }

            var extension = Path.GetExtension(name);
            if (!string.IsNullOrEmpty(extension))
            {
                name = name.Substring(0, name.Length - extension.Length);
            }

            return name + "-" + timestamp.ToString("yyyyMMdd-HHmmss") + BackupFileExtension;
        }

        private string BuildUniqueBackupPath(string targetDirectory)
        {
            var now = DateTime.Now;
            var baseName = BuildBackupFileName(Path.GetFileName(_store.FilePath), now);
            var candidate = Path.Combine(targetDirectory, baseName);

            for (var index = 2; File.Exists(candidate) && index <= MaxDuplicateIndex; index++)
            {
                var name = Path.GetFileNameWithoutExtension(baseName) + "-" + index + BackupFileExtension;
                candidate = Path.Combine(targetDirectory, name);
            }

            if (File.Exists(candidate))
            {
                throw new IOException("Не удалось подобрать имя файла копии: в каталоге слишком много копий за эту секунду.");
            }

            return candidate;
        }

        private string BuildSafetyCopyPath(string backupPath)
        {
            // Страховочную копию храним рядом с рабочим файлом, чтобы её было легко найти.
            var directory = Path.GetDirectoryName(_store.FilePath);
            var sourceName = Path.GetFileNameWithoutExtension(backupPath);
            var fileName = (string.IsNullOrEmpty(sourceName) ? "data" : sourceName) +
                           SafetyCopySuffix + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";

            return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
        }

        private static DataIntegrityReport BuildReport(DataFileInspection inspection)
        {
            var issues = new List<DataIntegrityIssue>();

            if (inspection.State == DataFileState.Corrupted)
            {
                issues.Add(new DataIntegrityIssue(
                    DataIntegrityIssueSeverity.Error,
                    "Файл повреждён",
                    string.IsNullOrWhiteSpace(inspection.ErrorMessage)
                        ? "Документ данных не соответствует ожидаемому формату."
                        : inspection.ErrorMessage));
            }
            else if (inspection.State == DataFileState.UnsupportedVersion)
            {
                issues.Add(new DataIntegrityIssue(
                    DataIntegrityIssueSeverity.Error,
                    "Неподдерживаемая версия формата",
                    "Версия файла " + inspection.SchemaVersion + " новее поддерживаемой " +
                    DataSchema.Version + ". Установите актуальную версию программы."));
            }
            else if (inspection.Data != null)
            {
                foreach (var formatIssue in inspection.Issues)
                {
                    issues.Add(new DataIntegrityIssue(
                        DataIntegrityIssueSeverity.Warning,
                        "Формат файла",
                        formatIssue));
                }

                foreach (var consistencyIssue in DataConsistencyInspector.Check(inspection.Data))
                {
                    issues.Add(new DataIntegrityIssue(
                        DataIntegrityIssueSeverity.Warning,
                        "Несогласованные данные",
                        consistencyIssue));
                }
            }

            return new DataIntegrityReport(
                inspection.FilePath,
                inspection.FileExists,
                MapLevel(inspection.State, issues),
                inspection.SchemaVersion > 0 ? (int?)inspection.SchemaVersion : null,
                DataSchema.Version,
                inspection.FileExists ? GetLastWriteTime(inspection.FilePath) : (DateTime?)null,
                inspection.Data == null ? 0 : inspection.Data.Ingredients.Count,
                inspection.Data == null ? 0 : inspection.Data.Movements.Count,
                issues);
        }

        private static DataIntegrityLevel MapLevel(DataFileState state, IList<DataIntegrityIssue> issues)
        {
            if (issues.Any(issue => issue.IsError))
            {
                return DataIntegrityLevel.Failed;
            }

            switch (state)
            {
                case DataFileState.Missing:
                    return DataIntegrityLevel.FileMissing;
                case DataFileState.NeedsUpgrade:
                    return DataIntegrityLevel.VersionUpgradePending;
                default:
                    return issues.Count > 0 ? DataIntegrityLevel.Warnings : DataIntegrityLevel.Ok;
            }
        }

        private static DateTime? GetLastWriteTime(string path)
        {
            try
            {
                return File.Exists(path) ? (DateTime?)File.GetLastWriteTime(path) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string FormatCounts(AppData data)
        {
            if (data == null)
            {
                return "нет данных";
            }

            return "позиций: " + data.Ingredients.Count + ", операций: " + data.Movements.Count;
        }

        private static BackupOperationResult Failure(string message)
        {
            return new BackupOperationResult(false, message, string.Empty, null, null, null);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
