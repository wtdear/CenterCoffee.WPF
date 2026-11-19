using System;
using System.Globalization;
using System.Text;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Application.Services
{
    /// <summary>
    /// Формирует CSV-представление складских отчётов.
    /// Формат рассчитан на открытие в русской версии Excel:
    /// разделитель «;», десятичная запятая, UTF-8 с BOM для кириллицы.
    /// Числа пишутся без разделителя тысяч, чтобы Excel не принял их за текст.
    /// </summary>
    public sealed class WarehouseReportCsvExporter : IWarehouseReportExporter
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        public string GetFileExtension()
        {
            return ".csv";
        }

        public string GetSuggestedFileName(WarehouseReportKind kind, DateTime from, DateTime to)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string reportName;

            switch (kind)
            {
                case WarehouseReportKind.Turnover:
                case WarehouseReportKind.Movements:
                    reportName = GetKindCsvName(kind) + "-" + from.ToString("yyyyMMdd", CultureInfo.InvariantCulture) +
                                 "-" + to.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    break;
                default:
                    reportName = GetKindCsvName(kind);
                    break;
            }

            return "sklad-" + reportName + "-" + stamp + ".csv";
        }

        private static string GetKindCsvName(WarehouseReportKind kind)
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

        public string BuildStockReport(StockReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Отчёт об остатках склада");
            builder.AppendLine("Сформирован;" + FormatMoment(report.GeneratedAt));
            builder.AppendLine("Позиций;" + Number(report.Ingredients.Count));
            builder.AppendLine("Ниже минимума;" + Number(report.LowStockCount));
            builder.AppendLine("Нет в наличии;" + Number(report.OutOfStockCount));
            builder.AppendLine("Стоимость запасов, ₽;" + Money(report.TotalValue));
            builder.AppendLine("Примечание;Стоимость — оценка по закупочной цене из карточки позиции, а не себестоимость.");
            builder.AppendLine();

            AppendRow(builder, "Наименование", "Единица", "Остаток", "Минимум", "Закупочная цена, ₽", "Стоимость, ₽", "Статус");

            foreach (var ingredient in report.Ingredients)
            {
                builder.AppendLine(Join(
                    ingredient.Name,
                    GetUnitName(ingredient.Unit),
                    Number(ingredient.Stock),
                    Number(ingredient.MinStock),
                    Money(ingredient.PurchasePrice),
                    Money(ingredient.StockValue),
                    GetStatusName(ingredient)));
            }

            return builder.ToString();
        }

        public string BuildTurnoverReport(TurnoverReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Отчёт об оборотах и расходе");
            builder.AppendLine("Период;" + Date(report.From) + " — " + Date(report.To));
            builder.AppendLine("Позиций с движением;" + Number(report.PositionsWithMovementCount));
            builder.AppendLine("Операций за период;" + Number(report.OperationsCount));
            builder.AppendLine();

            AppendRow(
                builder,
                "Наименование",
                "Единица",
                "Остаток на начало",
                "Приход",
                "Списание",
                "Остаток на конец");

            foreach (var item in report.Items)
            {
                builder.AppendLine(Join(
                    item.Name,
                    GetUnitName(item.Unit),
                    Number(item.OpeningStock),
                    Number(item.Received),
                    Number(item.WrittenOff),
                    Number(item.ClosingStock)));
            }

            return builder.ToString();
        }

        public string BuildDeficitReport(DeficitReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Отчёт о дефиците");
            builder.AppendLine("Сформирован;" + FormatMoment(report.GeneratedAt));
            builder.AppendLine("Позиций ниже минимума;" + Number(report.Items.Count));
            builder.AppendLine("Нет в наличии;" + Number(report.OutOfStockCount));
            builder.AppendLine();

            AppendRow(
                builder,
                "Наименование",
                "Единица",
                "Остаток",
                "Минимум",
                "Рекомендуется докупить",
                "Стоимость закупки, ₽",
                "Статус");

            foreach (var item in report.Items)
            {
                var ingredient = item.Ingredient;
                builder.AppendLine(Join(
                    ingredient.Name,
                    GetUnitName(ingredient.Unit),
                    Number(ingredient.Stock),
                    Number(ingredient.MinStock),
                    Number(item.RecommendedAmount),
                    Money(item.RecommendedAmount * ingredient.PurchasePrice),
                    GetStatusName(ingredient)));
            }

            return builder.ToString();
        }

        public string BuildMovementReport(MovementReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("Отчёт о движении склада");
            builder.AppendLine("Период;" + Date(report.From) + " — " + Date(report.To));
            builder.AppendLine("Операций всего;" + Number(report.Movements.Count));
            builder.AppendLine("Приход;" + Number(report.ReceiptCount));
            builder.AppendLine("Списание;" + Number(report.WriteOffCount));
            builder.AppendLine("Начальный остаток;" + Number(report.InitialCount));
            builder.AppendLine();

            AppendRow(
                builder,
                "Дата и время",
                "Наименование",
                "Тип",
                "Количество",
                "Единица",
                "Остаток после",
                "Комментарий");

            foreach (var movement in report.Movements)
            {
                builder.AppendLine(Join(
                    movement.CreatedAt.ToString("dd.MM.yyyy HH:mm", RussianCulture),
                    movement.IngredientName,
                    GetMovementTypeName(movement.Type),
                    Number(movement.Amount),
                    GetUnitName(movement.Unit),
                    Number(movement.StockAfter),
                    movement.Reason));
            }

            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, params string[] values)
        {
            builder.AppendLine(Join(values));
        }

        private static string Join(params string[] values)
        {
            var result = new string[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                result[index] = Escape(values[index]);
            }

            return string.Join(";", result);
        }

        /// <summary>
        /// Экранирует поле по правилам CSV: кавычки, разделитель и переводы строк
        /// внутри значения требуют обрамления, внутренние кавычки удваиваются.
        /// Значения, похожие на формулу, получают ведущую кавычку, чтобы Excel
        /// не выполнил их при открытии файла.
        /// </summary>
        private static string Escape(string value)
        {
            var text = value ?? string.Empty;

            if (text.Length > 0 && (text[0] == '=' || text[0] == '+' || text[0] == '-' || text[0] == '@') &&
                !LooksLikeNumber(text))
            {
                text = "'" + text;
            }

            if (text.IndexOf(';') >= 0 ||
                text.IndexOf('"') >= 0 ||
                text.IndexOf('\r') >= 0 ||
                text.IndexOf('\n') >= 0 ||
                text.StartsWith(" ", StringComparison.Ordinal) ||
                text.EndsWith(" ", StringComparison.Ordinal))
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }

            return text;
        }

        private static bool LooksLikeNumber(string text)
        {
            decimal parsed;
            return decimal.TryParse(text, NumberStyles.Number, RussianCulture, out parsed);
        }

        private static string Number(decimal value)
        {
            return value.ToString("0.###", RussianCulture);
        }

        private static string Number(int value)
        {
            return value.ToString(RussianCulture);
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
