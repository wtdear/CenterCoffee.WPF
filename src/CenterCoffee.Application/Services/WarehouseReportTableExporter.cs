using System;
using System.Globalization;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Application.Services
{
    /// <summary>
    /// Формирует табличное представление складских отчётов для выгрузки в Excel.
    /// Здесь же задаются ширины колонок, выравнивание и окраска строк: файл должен
    /// читаться глазами, а не только открываться. Запись файла выполняет
    /// <see cref="IReportExportWriter"/>, поэтому формат Excel в этом классе не упоминается.
    /// </summary>
    public sealed class WarehouseReportTableExporter : IWarehouseReportExporter
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        // Ширины подобраны так, чтобы длинные названия и комментарии читались целиком,
        // а короткие служебные колонки не растягивали таблицу.
        private const double NameWidth = 46;
        private const double CommentWidth = 52;
        private const double UnitWidth = 10;
        private const double QuantityWidth = 14;
        private const double MoneyWidth = 20;
        private const double StatusWidth = 20;
        private const double MomentWidth = 20;

        public string GetFileExtension()
        {
            return ".xlsx";
        }

        public string GetSuggestedFileName(WarehouseReportKind kind, DateTime from, DateTime to)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string reportName;

            switch (kind)
            {
                case WarehouseReportKind.Turnover:
                case WarehouseReportKind.Movements:
                    reportName = GetKindFileName(kind) + "-" +
                                 from.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" +
                                 to.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    break;
                default:
                    reportName = GetKindFileName(kind);
                    break;
            }

            return "sklad-" + reportName + "-" + stamp + ".xlsx";
        }

        private static string GetKindFileName(WarehouseReportKind kind)
        {
            switch (kind)
            {
                case WarehouseReportKind.Stock:
                    return "ostatki";
                case WarehouseReportKind.Turnover:
                    return "oboroty";
                case WarehouseReportKind.Deficit:
                    return "defitsit";
                default:
                    return "dvizhenie";
            }
        }

        public ReportTable BuildStockReport(StockReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var table = new ReportTable(
                "Отчёт об остатках склада",
                new[]
                {
                    new ReportColumn("Наименование", NameWidth, ReportCellKind.Text, ReportCellAlignment.Left),
                    new ReportColumn("Единица", UnitWidth, ReportCellKind.Text, ReportCellAlignment.Center),
                    new ReportColumn("Остаток", QuantityWidth, ReportCellKind.Number, ReportCellAlignment.Right),
                    new ReportColumn("Минимум", QuantityWidth, ReportCellKind.Number, ReportCellAlignment.Right),
                    new ReportColumn("Закупочная цена, ₽", MoneyWidth, ReportCellKind.Money, ReportCellAlignment.Right),
                    new ReportColumn("Стоимость, ₽", MoneyWidth, ReportCellKind.Money, ReportCellAlignment.Right),
                    new ReportColumn("Статус", StatusWidth, ReportCellKind.Text, ReportCellAlignment.Center)
                });

            table.AddSummary("Сформирован", FormatMoment(report.GeneratedAt));
            table.AddSummary("Позиций", report.Ingredients.Count);
            table.AddSummary("Ниже минимума", report.LowStockCount);
            table.AddSummary("Нет в наличии", report.OutOfStockCount);
            table.AddSummary("Стоимость запасов, ₽", Money(report.TotalValue));

            foreach (var ingredient in report.Ingredients)
            {
                table.AddRow(new ReportRow(GetTone(ingredient))
                    .Text(0, ingredient.Name)
                    .Text(1, GetUnitName(ingredient.Unit))
                    .Number(2, ingredient.Stock)
                    .Number(3, ingredient.MinStock)
                    .Money(4, ingredient.PurchasePrice)
                    .Money(5, ingredient.StockValue)
                    .Text(6, GetStatusName(ingredient)));
            }

            table.Note = "Стоимость — оценка по закупочной цене из карточки позиции, а не себестоимость.";
            return table;
        }

        public ReportTable BuildTurnoverReport(TurnoverReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var table = new ReportTable(
                "Отчёт об оборотах и расходе",
                new[]
                {
                    new ReportColumn("Наименование", NameWidth, ReportCellKind.Text, ReportCellAlignment.Left),
                    new ReportColumn("Единица", UnitWidth, ReportCellKind.Text, ReportCellAlignment.Center),
                    new ReportColumn("Остаток на начало", QuantityWidth + 4, ReportCellKind.Number, ReportCellAlignment.Right),
                    new ReportColumn("Приход", QuantityWidth, ReportCellKind.Delta, ReportCellAlignment.Right),
                    new ReportColumn("Списание", QuantityWidth, ReportCellKind.Delta, ReportCellAlignment.Right),
                    new ReportColumn("Остаток на конец", QuantityWidth + 4, ReportCellKind.Number, ReportCellAlignment.Right)
                });

            table.AddSummary("Период", Date(report.From) + " — " + Date(report.To));
            table.AddSummary("Позиций с движением", report.PositionsWithMovementCount);
            table.AddSummary("Операций за период", report.OperationsCount);

            foreach (var item in report.Items)
            {
                table.AddRow(new ReportRow(ReportRowTone.Default)
                    .Text(0, item.Name)
                    .Text(1, GetUnitName(item.Unit))
                    .Number(2, item.OpeningStock)
                    .Delta(3, item.Received)
                    .Delta(4, -item.WrittenOff)
                    .Number(5, item.ClosingStock));
            }

            return table;
        }

        public ReportTable BuildDeficitReport(DeficitReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var table = new ReportTable(
                "Отчёт о дефиците",
                new[]
                {
                    new ReportColumn("Наименование", NameWidth, ReportCellKind.Text, ReportCellAlignment.Left),
                    new ReportColumn("Единица", UnitWidth, ReportCellKind.Text, ReportCellAlignment.Center),
                    new ReportColumn("Остаток", QuantityWidth, ReportCellKind.Number, ReportCellAlignment.Right),
                    new ReportColumn("Минимум", QuantityWidth, ReportCellKind.Number, ReportCellAlignment.Right),
                    new ReportColumn("Рекомендуется докупить", QuantityWidth + 8, ReportCellKind.Number, ReportCellAlignment.Right),
                    new ReportColumn("Стоимость закупки, ₽", MoneyWidth + 2, ReportCellKind.Money, ReportCellAlignment.Right),
                    new ReportColumn("Статус", StatusWidth, ReportCellKind.Text, ReportCellAlignment.Center)
                });

            table.AddSummary("Сформирован", FormatMoment(report.GeneratedAt));
            table.AddSummary("Позиций ниже минимума", report.Items.Count);
            table.AddSummary("Нет в наличии", report.OutOfStockCount);

            foreach (var item in report.Items)
            {
                var ingredient = item.Ingredient;
                table.AddRow(new ReportRow(GetTone(ingredient))
                    .Text(0, ingredient.Name)
                    .Text(1, GetUnitName(ingredient.Unit))
                    .Number(2, ingredient.Stock)
                    .Number(3, ingredient.MinStock)
                    .Number(4, item.RecommendedAmount)
                    .Money(5, item.RecommendedAmount * ingredient.PurchasePrice)
                    .Text(6, GetStatusName(ingredient)));
            }

            return table;
        }

        public ReportTable BuildMovementReport(MovementReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var table = new ReportTable(
                "Отчёт о движении склада",
                new[]
                {
                    new ReportColumn("Дата и время", MomentWidth, ReportCellKind.Date, ReportCellAlignment.Center),
                    new ReportColumn("Наименование", NameWidth - 6, ReportCellKind.Text, ReportCellAlignment.Left),
                    new ReportColumn("Тип", StatusWidth - 2, ReportCellKind.Text, ReportCellAlignment.Center),
                    new ReportColumn("Количество", QuantityWidth, ReportCellKind.Delta, ReportCellAlignment.Right),
                    new ReportColumn("Единица", UnitWidth, ReportCellKind.Text, ReportCellAlignment.Center),
                    new ReportColumn("Остаток после", QuantityWidth + 2, ReportCellKind.Number, ReportCellAlignment.Right),
                    new ReportColumn("Комментарий", CommentWidth, ReportCellKind.Text, ReportCellAlignment.Left)
                });

            table.AddSummary("Период", Date(report.From) + " — " + Date(report.To));
            table.AddSummary("Операций всего", report.Movements.Count);
            table.AddSummary("Приход", report.ReceiptCount);
            table.AddSummary("Списание", report.WriteOffCount);
            table.AddSummary("Начальный остаток", report.InitialCount);

            foreach (var movement in report.Movements)
            {
                table.AddRow(new ReportRow(GetTone(movement.Type))
                    .Moment(0, movement.CreatedAt)
                    .Text(1, movement.IngredientName)
                    .Text(2, GetMovementTypeName(movement.Type))
                    .Delta(3, movement.Type == StockMovementType.WriteOff ? -movement.Amount : movement.Amount)
                    .Text(4, GetUnitName(movement.Unit))
                    .Number(5, movement.StockAfter)
                    .Text(6, string.IsNullOrWhiteSpace(movement.Reason) ? "Без комментария" : movement.Reason));
            }

            return table;
        }

        private static ReportRowTone GetTone(Ingredient ingredient)
        {
            if (ingredient.Stock <= 0m)
            {
                return ReportRowTone.Danger;
            }

            return ingredient.IsLowStock ? ReportRowTone.Warning : ReportRowTone.Default;
        }

        private static ReportRowTone GetTone(StockMovementType type)
        {
            switch (type)
            {
                case StockMovementType.Receipt:
                    return ReportRowTone.Positive;
                case StockMovementType.WriteOff:
                    return ReportRowTone.Danger;
                default:
                    return ReportRowTone.Muted;
            }
        }

        private static string Number(decimal value)
        {
            return value.ToString("0.###", RussianCulture);
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.00", RussianCulture);
        }

        private static string Date(DateTime value)
        {
            return value.ToString("dd.MM.yyyy", RussianCulture);
        }

        private static string FormatMoment(DateTime value)
        {
            return value.ToString("dd.MM.yyyy HH:mm", RussianCulture);
        }

        private static string GetStatusName(Ingredient ingredient)
        {
            if (ingredient.Stock <= 0m)
            {
                return "Нет в наличии";
            }

            return ingredient.IsLowStock ? "Ниже минимума" : "В норме";
        }

        private static string GetMovementTypeName(StockMovementType type)
        {
            switch (type)
            {
                case StockMovementType.Receipt:
                    return "Приход";
                case StockMovementType.WriteOff:
                    return "Списание";
                default:
                    return "Начальный остаток";
            }
        }

        private static string GetUnitName(MeasurementUnit unit)
        {
            switch (unit)
            {
                case MeasurementUnit.Gram:
                    return "г";
                case MeasurementUnit.Milliliter:
                    return "мл";
                case MeasurementUnit.Piece:
                    return "шт";
                default:
                    return string.Empty;
            }
        }
    }
}
