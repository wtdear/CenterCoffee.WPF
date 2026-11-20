using System;

namespace CenterCoffee.Application.Abstractions
{
    /// <summary>
    /// Представление складских отчётов в виде таблицы. Контракт вынесен отдельно
    /// от экрана, чтобы формат выгрузки не расползался по моделям представления.
    /// Сам файл не пишет: запись выполняет <see cref="IReportExportWriter"/>.
    /// </summary>
    public interface IWarehouseReportExporter
    {
        /// <summary>Расширение файла вместе с точкой, включая формат.</summary>
        string GetFileExtension();

        /// <summary>Имя файла выгрузки без каталога.</summary>
        string GetSuggestedFileName(WarehouseReportKind kind, DateTime from, DateTime to);

        ReportTable BuildStockReport(StockReport report);

        ReportTable BuildTurnoverReport(TurnoverReport report);

        ReportTable BuildDeficitReport(DeficitReport report);

        ReportTable BuildMovementReport(MovementReport report);
    }

    /// <summary>
    /// Запись файла выгрузки. Реализация скрыта в слое Infrastructure, поэтому
    /// модель представления не знает ни кодировки, ни способа записи.
    /// </summary>
    public interface IReportExportWriter
    {
        /// <summary>
        /// Записывает оформленный файл отчёта. Возвращает полный путь записанного файла.
        /// </summary>
        string Write(ReportTable table, string filePath);
    }
}