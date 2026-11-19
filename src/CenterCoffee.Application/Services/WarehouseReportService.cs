using System;
using System.Collections.Generic;
using System.Linq;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Common;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Application.Services
{
    /// <summary>
    /// Складские отчёты. Сервис только читает репозитории и не сохраняет изменений.
    /// Единое место расчёта показателей: границы периода, начальный и конечный
    /// остатки, сумма прихода и списания.
    /// </summary>
    public sealed class WarehouseReportService : IWarehouseReportService
    {
        private readonly IUnitOfWork _unitOfWork;

        public WarehouseReportService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public StockReport GetStockReport()
        {
            var ingredients = _unitOfWork.Ingredients
                .GetAll()
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
                .AsReadOnly();

            return new StockReport(
                ingredients,
                ingredients.Count(item => item.IsLowStock && item.Stock > 0m),
                ingredients.Count(item => item.Stock <= 0m),
                ingredients.Sum(item => item.StockValue),
                GetTimestamp());
        }

        public TurnoverReport GetTurnoverReport(DateTime from, DateTime to)
        {
            var range = ValidatePeriod(from, to);
            var ingredients = _unitOfWork.Ingredients.GetAll();
            var movements = _unitOfWork.Movements.GetAll();

            var movementsInPeriod = movements
                .Where(movement => IsInPeriod(movement.CreatedAt, range.FromMoment, range.ToMoment))
                .ToList();

            // Позиция показывается, если у неё есть движения в периоде либо ненулевой
            // начальный или конечный остаток. Строки без оборота только засоряют отчёт.
            var candidateIds = new HashSet<Guid>(
                movementsInPeriod.Select(movement => movement.IngredientId)
                    .Concat(ingredients
                        .Where(ingredient => ingredient.Stock != 0m)
                        .Select(ingredient => ingredient.Id)));

            var items = new List<TurnoverItem>();
            foreach (var ingredientId in candidateIds)
            {
                var ingredient = ingredients.FirstOrDefault(item => item.Id == ingredientId);
                var movementsForIngredient = movements
                    .Where(movement => movement.IngredientId == ingredientId)
                    .OrderBy(movement => movement.CreatedAt)
                    .ToList();

                var opening = CalculateStockAt(movementsForIngredient, range.FromMoment);
                var received = movementsForIngredient
                    .Where(movement => IsInPeriod(movement.CreatedAt, range.FromMoment, range.ToMoment) && IsIncoming(movement))
                    .Sum(movement => movement.Amount);
                var writtenOff = movementsForIngredient
                    .Where(movement => IsInPeriod(movement.CreatedAt, range.FromMoment, range.ToMoment) && IsOutgoing(movement))
                    .Sum(movement => movement.Amount);
                var closing = CalculateStockAt(movementsForIngredient, range.ToMoment.AddTicks(1));

                if (opening == 0m && received == 0m && writtenOff == 0m && closing == 0m)
                {
                    continue;
                }

                // Название берём из карточки, а если позиция удалена — из снимка в истории.
                var name = ingredient != null
                    ? ingredient.Name
                    : movementsForIngredient.Last().IngredientName;
                var unit = ingredient != null
                    ? ingredient.Unit
                    : movementsForIngredient.Last().Unit;

                items.Add(new TurnoverItem(
                    ingredientId,
                    name,
                    unit,
                    opening,
                    received,
                    writtenOff,
                    closing));
            }

            return new TurnoverReport(
                items
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
                    .AsReadOnly(),
                range.FromDate,
                range.ToDate,
                movementsInPeriod.Select(movement => movement.IngredientId).Distinct().Count(),
                movementsInPeriod.Count);
        }

        public DeficitReport GetDeficitReport()
        {
            var ingredients = _unitOfWork.Ingredients.GetAll();

            // То же правило, что и в доменной сущности (остаток <= минимум). Позиции
            // без минимального остатка в отчёт не попадают: докупать нечего.
            var items = ingredients
                .Where(ingredient => ingredient.IsLowStock && ingredient.MinStock > 0m)
                .OrderBy(ingredient => ingredient.Stock <= 0m ? 0 : 1)
                .ThenBy(ingredient => ingredient.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(ingredient => new DeficitItem(
                    ingredient,
                    Math.Max(0m, ingredient.MinStock - ingredient.Stock)))
                .ToList()
                .AsReadOnly();

            return new DeficitReport(
                items,
                ingredients.Count(ingredient => ingredient.Stock <= 0m),
                GetTimestamp());
        }

        public MovementReport GetMovementReport(DateTime from, DateTime to)
        {
            var range = ValidatePeriod(from, to);

            var movements = _unitOfWork.Movements
                .GetAll()
                .Where(movement => IsInPeriod(movement.CreatedAt, range.FromMoment, range.ToMoment))
                .OrderByDescending(movement => movement.CreatedAt)
                .ToList()
                .AsReadOnly();

            return new MovementReport(
                movements,
                range.FromDate,
                range.ToDate,
                movements.Count(movement => movement.Type == StockMovementType.Receipt),
                movements.Count(movement => movement.Type == StockMovementType.WriteOff),
                movements.Count(movement => movement.Type == StockMovementType.Initial));
        }

        public DateTime? GetEarliestMovementDate()
        {
            var movements = _unitOfWork.Movements.GetAll();
            if (movements.Count == 0)
            {
                return null;
            }

            return movements.Min(movement => movement.CreatedAt).Date;
        }

        /// <summary>Остаток перед моментом <paramref name="moment"/> по истории движений.</summary>
        private static decimal CalculateStockAt(IEnumerable<StockMovement> movements, DateTime moment)
        {
            var last = movements
                .Where(movement => IsBefore(movement.CreatedAt, moment))
                .OrderByDescending(movement => movement.CreatedAt)
                .FirstOrDefault();

            return last == null ? 0m : last.StockAfter;
        }

        private static bool IsIncoming(StockMovement movement)
        {
            return movement.Type == StockMovementType.Receipt ||
                   movement.Type == StockMovementType.Initial;
        }

        private static bool IsOutgoing(StockMovement movement)
        {
            return movement.Type == StockMovementType.WriteOff;
        }

        private static bool IsInPeriod(DateTime value, DateTime from, DateTime to)
        {
            return value >= from && value <= to;
        }

        private static bool IsBefore(DateTime value, DateTime moment)
        {
            return value < moment;
        }

        private static PeriodRange ValidatePeriod(DateTime from, DateTime to)
        {
            if (from == default(DateTime) || to == default(DateTime))
            {
                throw new DomainException("Не задан период отчёта.");
            }

            if (from.Date > to.Date)
            {
                throw new DomainException("Начало периода не может быть позже конца периода.");
            }

            // Границы приводим к целым дням: период включает оба дня целиком,
            // поэтому отчёт не теряет операции, сделанные позже в последний день.
            return new PeriodRange(from.Date, to.Date);
        }

        private static DateTime GetTimestamp()
        {
            // Время фиксируется в момент формирования отчёта, чтобы заголовок
            // отчёта не «плыл» при каждом обращении к свойству.
            return DateTime.Now;
        }

        private struct PeriodRange
        {
            public PeriodRange(DateTime fromDate, DateTime toDate)
            {
                FromDate = fromDate;
                ToDate = toDate;
                FromMoment = fromDate;
                ToMoment = toDate.AddDays(1).AddTicks(-1);
            }

            /// <summary>Дата начала периода для подписи.</summary>
            public DateTime FromDate { get; private set; }

            /// <summary>Дата конца периода для подписи.</summary>
            public DateTime ToDate { get; private set; }

            public DateTime FromMoment { get; private set; }

            public DateTime ToMoment { get; private set; }
        }
    }
}
