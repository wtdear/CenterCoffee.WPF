using System;
using System.Collections.Generic;

namespace CenterCoffee.Application.Abstractions
{
    /// <summary>
    /// Уровень достоверности сведений о файле данных.
    /// </summary>
    public enum DataIntegrityLevel
    {
        /// <summary>Файл отсутствует — это допустимое начальное состояние пустой установки.</summary>
        FileMissing = 0,

        /// <summary>Файл прочитан, версия формата поддерживается, замечаний нет.</summary>
        Ok = 1,

        /// <summary>Файл хранится в старой версии формата; при следующем сохранении он будет обновлён.</summary>
        VersionUpgradePending = 2,

        /// <summary>Данные читаются, но обнаружены несогласованности, которые нужно проверить.</summary>
        Warnings = 3,

        /// <summary>Файл нельзя безопасно использовать.</summary>
        Failed = 4
    }

    public enum DataIntegrityIssueSeverity
    {
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// Одно замечание проверки целостности. Описание содержит только диагностику,
    /// пригодную для чтения пользователем, но не сырые пути и стек вызовов.
    /// </summary>
    public sealed class DataIntegrityIssue
    {
        public DataIntegrityIssue(DataIntegrityIssueSeverity severity, string title, string details)
        {
            Severity = severity;
            Title = title ?? string.Empty;
            Details = details ?? string.Empty;
        }

        public DataIntegrityIssueSeverity Severity { get; private set; }

        public string Title { get; private set; }

        public string Details { get; private set; }

        public bool IsError
        {
            get { return Severity == DataIntegrityIssueSeverity.Error; }
        }
    }

    /// <summary>
    /// Результат проверки файла данных: версия формата, объём данных и список замечаний.
    /// </summary>
    public sealed class DataIntegrityReport
    {
        private readonly List<DataIntegrityIssue> _issues;

        public DataIntegrityReport(
            string filePath,
            bool fileExists,
            DataIntegrityLevel level,
            int? schemaVersion,
            int supportedSchemaVersion,
            DateTime? lastWriteTime,
            int ingredientsCount,
            int stockMovementsCount,
            IEnumerable<DataIntegrityIssue> issues)
        {
            FilePath = filePath ?? string.Empty;
            FileExists = fileExists;
            Level = level;
            SchemaVersion = schemaVersion;
            SupportedSchemaVersion = supportedSchemaVersion;
            LastWriteTime = lastWriteTime;
            IngredientsCount = ingredientsCount;
            StockMovementsCount = stockMovementsCount;
            _issues = issues == null ? new List<DataIntegrityIssue>() : new List<DataIntegrityIssue>(issues);
        }

        public string FilePath { get; private set; }

        public bool FileExists { get; private set; }

        public DataIntegrityLevel Level { get; private set; }

        public int? SchemaVersion { get; private set; }

        public int SupportedSchemaVersion { get; private set; }

        public DateTime? LastWriteTime { get; private set; }

        public int IngredientsCount { get; private set; }

        public int StockMovementsCount { get; private set; }

        public IReadOnlyList<DataIntegrityIssue> Issues
        {
            get { return _issues.AsReadOnly(); }
        }

        public bool HasWarnings
        {
            get { return _issues.Count > 0; }
        }
    }
}
