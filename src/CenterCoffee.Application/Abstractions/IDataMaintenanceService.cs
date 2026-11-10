using System;
using System.Collections.Generic;

namespace CenterCoffee.Application.Abstractions
{
    /// <summary>
    /// Итог создания или восстановления резервной копии.
    /// </summary>
    public sealed class BackupOperationResult
    {
        public BackupOperationResult(
            bool success,
            string message,
            string backupPath,
            DateTime? createdAt,
            int? schemaVersion,
            IEnumerable<DataIntegrityIssue> issues)
            : this(success, false, message, backupPath, createdAt, schemaVersion, issues)
        {
        }

        public BackupOperationResult(
            bool success,
            bool requiresRestart,
            string message,
            string backupPath,
            DateTime? createdAt,
            int? schemaVersion,
            IEnumerable<DataIntegrityIssue> issues)
        {
            Success = success;
            RequiresRestart = requiresRestart;
            Message = message ?? string.Empty;
            BackupPath = backupPath ?? string.Empty;
            CreatedAt = createdAt;
            SchemaVersion = schemaVersion;
            Issues = issues == null
                ? new List<DataIntegrityIssue>().AsReadOnly()
                : new List<DataIntegrityIssue>(issues).AsReadOnly();
        }

        public bool Success { get; private set; }

        /// <summary>
        /// Файл данных уже заменён, но приложение не смогло перечитать данные.
        /// Работа со складом должна быть заблокирована до перезапуска: сохранение
        /// старых данных из памяти затёрло бы восстановленные.
        /// </summary>
        public bool RequiresRestart { get; private set; }

        public string Message { get; private set; }

        public string BackupPath { get; private set; }

        public DateTime? CreatedAt { get; private set; }

        public int? SchemaVersion { get; private set; }

        public IReadOnlyList<DataIntegrityIssue> Issues { get; private set; }

        public bool HasIssues
        {
            get { return Issues.Count > 0; }
        }
    }

    /// <summary>
    /// Проверка целостности локального файла данных и резервное копирование отдельным файлом.
    /// Реализация скрыта за этим контрактом, чтобы интерфейс не зависел от формата хранения.
    /// </summary>
    public interface IDataMaintenanceService
    {
        /// <summary>Путь к локальному файлу данных.</summary>
        string DataFilePath { get; }

        /// <summary>Поддерживаемая приложением версия формата.</summary>
        int SupportedSchemaVersion { get; }

        /// <summary>
        /// Проверяет файл данных, не изменяя его. Файл не переписывается и не повышается
        /// в версии: повышение происходит только при следующем обычном сохранении.
        /// </summary>
        DataIntegrityReport InspectData();

        /// <summary>Проверяет содержимое файла резервной копии, не подменяя им рабочие данные.</summary>
        DataIntegrityReport InspectBackup(string backupPath);

        /// <summary>
        /// Создаёт резервную копию отдельным файлом в выбранном каталоге.
        /// Имя копии содержит дату и время создания, поэтому прошлые копии не затираются.
        /// </summary>
        BackupOperationResult CreateBackup(string targetDirectory);

        /// <summary>
        /// Заменяет рабочие данные содержимым резервной копии. Перед заменой создаётся
        /// страховочная копия текущего файла, чтобы операцию можно было отменить.
        /// </summary>
        BackupOperationResult RestoreFromBackup(string backupPath);
    }
}
