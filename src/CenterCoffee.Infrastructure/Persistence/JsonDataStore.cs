using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Infrastructure.Persistence
{
    /// <summary>
    /// Чтение и атомарная запись локального JSON-документа.
    /// </summary>
    public sealed class JsonDataStore
    {
        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(AppData));

        private readonly string _filePath;

        public JsonDataStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Не задан путь к файлу данных.", nameof(filePath));
            }

            _filePath = filePath;
        }

        public string FilePath => _filePath;

        public bool Exists => File.Exists(_filePath);

        /// <summary>
        /// Читает файл для обычной работы приложения. Повреждённый, несовместимый или
        /// неоднозначный файл не подменяется пустой базой — вместо этого выбрасывается
        /// исключение, чтобы пользователь не принял потерю данных за чистый старт.
        /// </summary>
        public AppData Load()
        {
            var inspection = Inspect();

            switch (inspection.State)
            {
                case DataFileState.Missing:
                    return new AppData();

                case DataFileState.Loaded:
                case DataFileState.NeedsUpgrade:
                    inspection.Data.EnsureCollections();
                    return inspection.Data;

                case DataFileState.UnsupportedVersion:
                    throw new InvalidOperationException(
                        "Файл данных создан более новой версией приложения (версия формата " +
                        inspection.SchemaVersion + ", поддерживается " + DataSchema.Version + "). " +
                        "Установите актуальную версию программы или восстановите данные из резервной копии. " +
                        "Файл: " + _filePath);

                default:
                    throw new InvalidOperationException(
                        "Не удалось прочитать локальную базу данных: файл повреждён или имеет неверный формат. " +
                        "Путь: " + _filePath + ". " + inspection.ErrorMessage);
            }
        }

        /// <summary>
        /// Разбирает файл и определяет его состояние. Метод не выбрасывает исключений по
        /// содержимому файла: повреждение описывается в результате, чтобы его можно было
        /// показать пользователю и предложить восстановление из резервной копии.
        /// </summary>
        public DataFileInspection Inspect()
        {
            if (!File.Exists(_filePath))
            {
                return DataFileInspection.MissingFile(_filePath);
            }

            AppData data;
            try
            {
                using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    data = Serializer.ReadObject(stream) as AppData;
                }
            }
            catch (Exception exception) when (
                exception is SerializationException ||
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException)
            {
                return new DataFileInspection(
                    _filePath,
                    true,
                    DataFileState.Corrupted,
                    null,
                    0,
                    DescribeReadError(exception),
                    null);
            }

            if (data == null)
            {
                return new DataFileInspection(
                    _filePath,
                    true,
                    DataFileState.Corrupted,
                    null,
                    0,
                    "Файл не содержит документа данных.",
                    null);
            }

            var issues = new List<string>();

            if (data.SchemaVersion > DataSchema.Version)
            {
                return new DataFileInspection(
                    _filePath,
                    true,
                    DataFileState.UnsupportedVersion,
                    data,
                    data.SchemaVersion,
                    "Версия формата файла новее поддерживаемой.",
                    issues);
            }

            // Пустые коллекции и null-элементы — повреждение документа, а не состояние
            // бизнес-данных: дальше по коду такие значения привели бы к сбою при
            // сохранении или копировании, поэтому файл считается непригодным.
            if (data.Ingredients == null || data.Movements == null ||
                data.Ingredients.Any(item => item == null) ||
                data.Movements.Any(item => item == null))
            {
                return new DataFileInspection(
                    _filePath,
                    true,
                    DataFileState.Corrupted,
                    null,
                    data.SchemaVersion,
                    "Документ данных неполный: отсутствует список позиций или операций либо в списке есть пустые записи.",
                    issues);
            }

            if (data.SchemaVersion < DataSchema.MinimumSupportedVersion)
            {
                issues.Add("В файле не указана версия формата (schemaVersion). " +
                           "Файл будет переведён в текущую версию при следующем сохранении.");
                return new DataFileInspection(
                    _filePath,
                    true,
                    DataFileState.NeedsUpgrade,
                    data,
                    data.SchemaVersion,
                    string.Empty,
                    issues);
            }

            return new DataFileInspection(
                _filePath,
                true,
                DataFileState.Loaded,
                data,
                data.SchemaVersion,
                string.Empty,
                issues);
        }

        public void Save(AppData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            WriteAtomically(_filePath, CreateSnapshot(data), false);
        }

        /// <summary>
        /// Копия документа в текущей версии формата. Запись идёт по копии, чтобы
        /// изменение версии формата не влияло на загруженные в память данные.
        /// </summary>
        private static AppData CreateSnapshot(AppData data)
        {
            data.EnsureCollections();
            return new AppData
            {
                SchemaVersion = DataSchema.Version,
                Ingredients = new List<Ingredient>(data.Ingredients),
                Movements = new List<StockMovement>(data.Movements)
            };
        }

        /// <summary>
        /// Записывает документ в указанный путь, не затирая существующий файл.
        /// Используется для отдельных файлов резервной копии. Записывается копия документа,
        /// поэтому версия формата в переданном объекте не меняется.
        /// </summary>
        public void SaveTo(string targetPath, AppData data)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("Не задан путь к файлу.", nameof(targetPath));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            WriteAtomically(targetPath, CreateSnapshot(data), true);
        }

        /// <summary>
        /// Заменяет рабочий файл содержимым другого файла. Метод проверяет целостность
        /// источника до замены, создаёт страховочную копию заменяемого файла и делает замену
        /// атомарной: при сбое остаётся либо старый, либо новый файл, но не пустой.
        /// </summary>
        public void Replace(JsonDataStore source, string safetyCopyPath)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var inspection = source.Inspect();
            if (inspection.State != DataFileState.Loaded && inspection.State != DataFileState.NeedsUpgrade)
            {
                throw new InvalidOperationException(
                    "Резервную копию нельзя восстановить: файл повреждён или несовместим. " +
                    inspection.ErrorMessage);
            }

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".restore";

            try
            {
                File.Copy(source.FilePath, temporaryPath, true);

                // Проверяем копию ещё раз по месту, чтобы замена шла только проверенным файлом.
                var store = new JsonDataStore(temporaryPath);
                var copied = store.Inspect();
                if (copied.State != DataFileState.Loaded && copied.State != DataFileState.NeedsUpgrade)
                {
                    throw new InvalidOperationException(
                        "Резервную копию нельзя восстановить: после копирования файл оказался повреждён.");
                }

                if (File.Exists(_filePath))
                {
                    if (string.IsNullOrWhiteSpace(safetyCopyPath))
                    {
                        throw new ArgumentException("Не задан путь для страховочной копии.", nameof(safetyCopyPath));
                    }

                    WriteSafetyCopy(_filePath, safetyCopyPath);
                }

                if (File.Exists(_filePath))
                {
                    File.Replace(temporaryPath, _filePath, null);
                }
                else
                {
                    File.Move(temporaryPath, _filePath);
                }
            }
            finally
            {
                CleanupTemporary(temporaryPath);
            }
        }

        /// <summary>
        /// Страховочная копия пишется через временный файл и подмену, чтобы даже при
        /// сбое не осталось обрезанное «почти рабочее» состояние.
        /// </summary>
        private static void WriteSafetyCopy(string sourcePath, string targetPath)
        {
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var temporaryPath = targetPath + ".tmp";

            try
            {
                File.Copy(sourcePath, temporaryPath, true);

                if (File.Exists(targetPath))
                {
                    File.Replace(temporaryPath, targetPath, null);
                }
                else
                {
                    File.Move(temporaryPath, targetPath);
                }
            }
            finally
            {
                CleanupTemporary(temporaryPath);
            }
        }

        /// <summary>
        /// Удаляет временный файл, не скрывая исходную ошибку операции. Сбой уборки
        /// не должен подменять причину, из-за которой был вызван finally.
        /// </summary>
        private static void CleanupTemporary(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteAtomically(string path, AppData data, bool failIfExists)
        {
            if (failIfExists && File.Exists(path))
            {
                throw new IOException("Файл уже существует: " + path);
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = path + ".tmp";

            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Serializer.WriteObject(stream, data);
                    stream.Flush(true);
                }

                if (failIfExists && File.Exists(path))
                {
                    throw new IOException("Файл уже существует: " + path);
                }

                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                CleanupTemporary(temporaryPath);
            }
        }

        private static string DescribeReadError(Exception exception)
        {
            if (exception is UnauthorizedAccessException)
            {
                return "Нет прав на чтение файла.";
            }

            if (exception is IOException)
            {
                return "Файл занят другим процессом или недоступен для чтения.";
            }

            return "Содержимое файла не является корректным документом данных.";
        }
    }
}
