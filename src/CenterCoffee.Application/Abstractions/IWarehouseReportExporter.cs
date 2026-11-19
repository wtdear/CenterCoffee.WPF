using System;

namespace CenterCoffee.Application.Abstractions
{
    /// <summary>
    /// Представление складских отчётов в виде CSV-текста. Контракт вынесен отдельно
    /// от экрана, чтобы формат выгрузки не расползался по моделям представления.
    /// Сам по себе файл не пишет: запись выполняет <see cref="IReportExportWriter"/>.
    /// </summary>
    public interface IWarehouseReportExporter
    {
        /// <summary>Расширение файла вместе с точкой, включая формат.</summary>
        string GetFileExtension();

        /// <summary>Имя файла выгрузки без каталога.</summary>
        string GetSuggestedFileName(WarehouseReportKind kind, DateTime from, DateTime to);

        string BuildStockReport(StockReport report);

        string BuildTurnoverReport(TurnoverReport report);

        string BuildDeficitReport(DeficitReport report);

        string BuildMovementReport(MovementReport report);
    }

    /// <summary>
    /// Запись файла выгрузки. Реализация скрыта в слое Infrastructure, поэтому
    /// модель представления не знает ни кодировки, ни способа записи.
    /// </summary>
    public interface IReportExportWriter
    {
        /// <summary>
        /// Записывает текстовый файл в кодировке UTF-8 с BOM, чтобы кириллица
        /// корректно открывалась в Excel. Возвращает полный путь записанного файла.
        /// </summary>
        string WriteTextFile(string filePath, string content);
    }
}
