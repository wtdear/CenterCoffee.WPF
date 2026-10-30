using System;
using System.IO;
using System.Runtime.Serialization.Json;

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

        public AppData Load()
        {
            if (!File.Exists(_filePath))
            {
                return new AppData();
            }

            try
            {
                using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var data = Serializer.ReadObject(stream) as AppData;
                    if (data == null)
                    {
                        return new AppData();
                    }

                    data.EnsureCollections();
                    return data;
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is System.Runtime.Serialization.SerializationException)
            {
                throw new InvalidOperationException(
                    "Не удалось прочитать локальную базу данных по пути: " + _filePath,
                    exception);
            }
        }

        public void Save(AppData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".tmp";

            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Serializer.WriteObject(stream, data);
                    stream.Flush();
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
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
