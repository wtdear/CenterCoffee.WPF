using System;
using System.Collections.Generic;

namespace CenterCoffee.Infrastructure.Persistence
{
    /// <summary>
    /// Версии формата локального файла данных.
    /// </summary>
    public static class DataSchema
    {
        /// <summary>
        /// Версия 1 — первый поддерживаемый формат. Приложение не утверждает, что знает,
        /// как читать файлы более новых версий: это защищает от молчаливой потери данных.
        /// </summary>
        public const int Version = 1;

        public const int MinimumSupportedVersion = 1;
    }

    public enum DataFileState
    {
        /// <summary>Файл отсутствует — допустимое состояние пустой установки.</summary>
        Missing = 0,

        /// <summary>Файл повреждён и не может быть прочитан.</summary>
        Corrupted = 1,

        /// <summary>Файл прочитан, версия формата поддерживается.</summary>
        Loaded = 2,

        /// <summary>Файл прочитан, но хранится в более старой версии формата.</summary>
        NeedsUpgrade = 3,

        /// <summary>Версия формата новее поддерживаемой приложением.</summary>
        UnsupportedVersion = 4
    }

    /// <summary>
    /// Результат низкоуровневого разбора файла данных: без смешивания с бизнес-проверками.
    /// </summary>
    public sealed class DataFileInspection
    {
        public DataFileInspection(
            string filePath,
            bool fileExists,
            DataFileState state,
            AppData data,
            int schemaVersion,
            string errorMessage,
            IList<string> issues)
        {
            FilePath = filePath ?? string.Empty;
            FileExists = fileExists;
            State = state;
            Data = data;
            SchemaVersion = schemaVersion;
            ErrorMessage = errorMessage ?? string.Empty;
            Issues = issues == null ? new List<string>() : new List<string>(issues);
        }

        public string FilePath { get; private set; }

        public bool FileExists { get; private set; }

        public DataFileState State { get; private set; }

        /// <summary>Прочитанные данные; null, если файл отсутствует или повреждён.</summary>
        public AppData Data { get; private set; }

        /// <summary>Версия из файла; 0, если версия не указана.</summary>
        public int SchemaVersion { get; private set; }

        /// <summary>Диагностическое сообщение для повреждённого или неподдерживаемого файла.</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>Замечания формата, замеченные при чтении (например, отсутствие версии).</summary>
        public IList<string> Issues { get; private set; }

        public static DataFileInspection MissingFile(string filePath)
        {
            return new DataFileInspection(filePath, false, DataFileState.Missing, null, 0, string.Empty, null);
        }
    }
}
