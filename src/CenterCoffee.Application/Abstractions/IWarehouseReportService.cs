using System;
using System.Collections.Generic;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Application.Abstractions
{
    /// <summary>
    /// Виды складских отчётов первой версии. Состав согласован заказчиком:
    /// остатки на дату, обороты за период, дефицит и история движений за период.
    /// </summary>
    public enum WarehouseReportKind
    {
        Stock = 1,
        Turnover = 2,
        Deficit = 3,
        Movements = 4
    }

    /// <summary>
    /// Строка отчёта о дефиците: позиция и количество, которое нужно докупить до минимума.
    /// </summary>
    public sealed class DeficitItem
    {
        public DeficitItem(Ingredient ingredient, decimal recommendedAmount)
        {
            if (ingredient == null)
            {
                throw new ArgumentNullException(nameof(ingredient));
            }

            Ingredient = ingredient;
            RecommendedAmount = recommendedAmount;
        }

        /// <summary>Позиция склада на момент формирования отчёта.</summary>
        public Ingredient Ingredient { get; private set; }

        /// <summary>Сколько нужно докупить, чтобы достичь минимального остатка.</summary>
        public decimal RecommendedAmount { get; private set; }
    }

    /// <summary>
    /// Строка отчёта об оборотах: движение по одной позиции за период.
    /// Начальный и конечный остатки рассчитываются из истории движений.
    /// </summary>
    public sealed class TurnoverItem
    {
        public TurnoverItem(
            Guid ingredientId,
            string name,
            MeasurementUnit unit,
            decimal openingStock,
            decimal received,
            decimal writtenOff,
            decimal closingStock)
        {
            IngredientId = ingredientId;
            Name = name;
            Unit = unit;
            OpeningStock = openingStock;
            Received = received;
            WrittenOff = writtenOff;
            ClosingStock = closingStock;
        }

        public Guid IngredientId { get; private set; }

        public string Name { get; private set; }

        public MeasurementUnit Unit { get; private set; }

        /// <summary>Остаток на начало периода.</summary>
        public decimal OpeningStock { get; private set; }

        /// <summary>Приход за период, включая начальный остаток новой позиции.</summary>
        public decimal Received { get; private set; }

        /// <summary>Списание за период.</summary>
        public decimal WrittenOff { get; private set; }

        /// <summary>Остаток на конец периода.</summary>
        public decimal ClosingStock { get; private set; }
    }

    /// <summary>Отчёт об остатках склада на дату формирования.</summary>
    public sealed class StockReport
    {
        public StockReport(
            IReadOnlyList<Ingredient> ingredients,
            int lowStockCount,
            int outOfStockCount,
            decimal totalValue,
            DateTime generatedAt)
        {
            Ingredients = ingredients ?? throw new ArgumentNullException(nameof(ingredients));
            LowStockCount = lowStockCount;
            OutOfStockCount = outOfStockCount;
            TotalValue = totalValue;
            GeneratedAt = generatedAt;
        }

        public IReadOnlyList<Ingredient> Ingredients { get; private set; }

        /// <summary>Позиции ниже минимума, у которых ещё есть остаток.</summary>
        public int LowStockCount { get; private set; }

        public int OutOfStockCount { get; private set; }

        /// <summary>Стоимость запасов по закупочной цене позиций (оценка).</summary>
        public decimal TotalValue { get; private set; }

        public DateTime GeneratedAt { get; private set; }
    }

    /// <summary>Отчёт об оборотах и расходе за период.</summary>
    public sealed class TurnoverReport
    {
        public TurnoverReport(
            IReadOnlyList<TurnoverItem> items,
            DateTime from,
            DateTime to,
            int positionsWithMovementCount,
            int operationsCount)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            From = from;
            To = to;
            PositionsWithMovementCount = positionsWithMovementCount;
            OperationsCount = operationsCount;
        }

        public IReadOnlyList<TurnoverItem> Items { get; private set; }

        public DateTime From { get; private set; }

        public DateTime To { get; private set; }

        /// <summary>Сколько позиций имели движения за период.</summary>
        public int PositionsWithMovementCount { get; private set; }

        /// <summary>Сколько операций прихода и списания прошло за период.</summary>
        public int OperationsCount { get; private set; }
    }

    /// <summary>Отчёт о дефиците: позиции на минимуме или ниже.</summary>
    public sealed class DeficitReport
    {
        public DeficitReport(IReadOnlyList<DeficitItem> items, int outOfStockCount, DateTime generatedAt)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            OutOfStockCount = outOfStockCount;
            GeneratedAt = generatedAt;
        }

        public IReadOnlyList<DeficitItem> Items { get; private set; }

        public int OutOfStockCount { get; private set; }

        public DateTime GeneratedAt { get; private set; }
    }

    /// <summary>Отчёт о движениях склада за период.</summary>
    public sealed class MovementReport
    {
        public MovementReport(
            IReadOnlyList<StockMovement> movements,
            DateTime from,
            DateTime to,
            int receiptCount,
            int writeOffCount,
            int initialCount)
        {
            Movements = movements ?? throw new ArgumentNullException(nameof(movements));
            From = from;
            To = to;
            ReceiptCount = receiptCount;
            WriteOffCount = writeOffCount;
            InitialCount = initialCount;
        }

        public IReadOnlyList<StockMovement> Movements { get; private set; }

        public DateTime From { get; private set; }

        public DateTime To { get; private set; }

        public int ReceiptCount { get; private set; }

        public int WriteOffCount { get; private set; }

        public int InitialCount { get; private set; }
    }

    /// <summary>
    /// Сценарии чтения складских отчётов. Все показатели считаются здесь, а не в
    /// модели представления, чтобы один и тот же отчёт не считался по-разному на
    /// разных экранах. Отчёты только читают данные и ничего не меняют.
    /// </summary>
    public interface IWarehouseReportService
    {
        /// <summary>Остатки склада на дату формирования отчёта.</summary>
        StockReport GetStockReport();

        /// <summary>Обороты и расход по каждой позиции за указанный период.</summary>
        TurnoverReport GetTurnoverReport(DateTime from, DateTime to);

        /// <summary>Позиции, остаток которых на минимуме или ниже.</summary>
        DeficitReport GetDeficitReport();

        /// <summary>История движений за период, новые сверху.</summary>
        MovementReport GetMovementReport(DateTime from, DateTime to);

        /// <summary>
        /// Самая ранняя дата операции в истории склада. Для пресета «За всё время»:
        /// без движений возвращает <c>null</c>, и вкладка показывает пустой отчёт
        /// вместо выдуманной даты начала.
        /// </summary>
        DateTime? GetEarliestMovementDate();
    }
}
