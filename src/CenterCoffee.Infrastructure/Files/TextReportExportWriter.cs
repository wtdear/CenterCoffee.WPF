using System;
using System.IO;
using System.Text;
using CenterCoffee.Application.Abstractions;

namespace CenterCoffee.Infrastructure.Files
{
    /// <summary>
    /// Запись выгрузок отчётов. Файл пишется через временный файл и подмену, чтобы
    /// при сбое не оставалось обрезанного «почти готового» отчёта. Существующий файл
    /// не затирается молча: вызывающая сторона передаёт уникальное имя.
    /// </summary>
    public sealed class TextReportExportWriter : IReportExportWriter    {
        // BOM нужен, чтобы Excel открыл кириллицу в UTF-8 без ручного выбора кодировки.
        private static readonly Encoding Utf8WithBom = new UTF8Encoding(true);

        public string WriteTextFile(string filePath, string content)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Не задан путь для сохранения отчёта.", nameof(filePath));
            }

            var fullPath = Path.GetFullPath(filePath);

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = fullPath + ".tmp";

            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, Utf8WithBom))
                    {
                        writer.Write(content ?? string.Empty);
                        writer.Flush();

                        // Сброс на диск — до закрытия потока: после Dispose поток уже закрыт.
                        stream.Flush(true);
                    }
                }

                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, null);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                CleanupTemporary(temporaryPath);
            }

            return fullPath;
        }

        /// <summary>
        /// Удаляет временный файл, не скрывая исходную ошибку записи: сбой уборки
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
    }
}
