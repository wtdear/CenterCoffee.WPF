using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Infrastructure.Persistence
{
    /// <summary>
    /// Проверяет бизнес-согласованность документа данных: связи, уникальность, допустимые
    /// значения и сходимость остатка с историей. Это замечания, а не отказ: файл читается,
    /// но требует решения пользователя.
    /// </summary>
    internal static class DataConsistencyInspector
    {
        private const int MaxListedItems = 5;

        public static IList<string> Check(AppData data)
        {
            var issues = new List<string>();
            if (data == null)
            {
                return issues;
            }

            // Пустые записи отсеиваются уже при разборе файла, но проверка не должна
            // падать, если её вызовут с неполным документом.
            var ingredients = (data.Ingredients ?? new List<Ingredient>())
                .Where(item => item != null)
                .ToList();

            var movements = (data.Movements ?? new List<StockMovement>())
                .Where(item => item != null)
                .ToList();

            CheckIngredients(ingredients, issues);
            CheckMovements(ingredients, movements, issues);
            CheckStockReconciliation(ingredients, movements, issues);
            return issues;
        }

        private static void CheckIngredients(IEnumerable<Ingredient> ingredients, IList<string> issues)
        {
            var invalid = ingredients
                .Where(item =>
                    item.Id == Guid.Empty ||
                    string.IsNullOrWhiteSpace(item.Name) ||
                    !Enum.IsDefined(typeof(MeasurementUnit), item.Unit) ||
                    item.Stock < 0m ||
                    item.MinStock < 0m ||
                    item.PurchasePrice < 0m)
                .ToList();

            if (invalid.Count > 0)
            {
                issues.Add(Format(
                    "Позиции с некорректными данными: " + invalid.Count + " — ",
                    invalid.Select(item => item.Name)));
            }

            var duplicates = ingredients
                .GroupBy(item => (item.Name ?? string.Empty).Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                issues.Add("Повторяющиеся названия складских позиций: " +
                           string.Join(", ", duplicates.Take(MaxListedItems)) +
                           (duplicates.Count > MaxListedItems ? " и др." : string.Empty) + ".");
            }
        }

        private static void CheckMovements(
            IEnumerable<Ingredient> ingredients,
            IEnumerable<StockMovement> movements,
            IList<string> issues)
        {
            var ingredientIds = new HashSet<Guid>(ingredients.Select(item => item.Id));

            var orphans = movements.Where(movement => !ingredientIds.Contains(movement.IngredientId)).ToList();
            if (orphans.Count > 0)
            {
                issues.Add(Format(
                    "Операции без складской позиции: " + orphans.Count + " — ",
                    orphans.Select(movement => movement.IngredientName)));
            }

            var invalid = movements
                .Where(movement =>
                    movement.Id == Guid.Empty ||
                    movement.IngredientId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(movement.IngredientName) ||
                    !Enum.IsDefined(typeof(MeasurementUnit), movement.Unit) ||
                    !Enum.IsDefined(typeof(StockMovementType), movement.Type) ||
                    movement.Amount <= 0m ||
                    movement.StockAfter < 0m ||
                    movement.CreatedAt == default(DateTime))
                .ToList();

            if (invalid.Count > 0)
            {
                issues.Add(Format(
                    "Операции с некорректными данными: " + invalid.Count + " — ",
                    invalid.Select(movement => movement.IngredientName)));
            }
        }

        /// <summary>
        /// Сверяет текущий остаток позиции с последней записью истории. Расхождение означает,
        /// что файл правили вручную или операция прошла не полностью — это повод показать
        /// предупреждение, а не автоматически «исправить» остаток. При равном времени
        /// последней считается запись, стоящая в файле позже.
        /// </summary>
        private static void CheckStockReconciliation(
            IEnumerable<Ingredient> ingredients,
            IEnumerable<StockMovement> movements,
            IList<string> issues)
        {
            var lastByIngredient = new Dictionary<Guid, StockMovement>();
            foreach (var movement in movements)
            {
                StockMovement current;
                if (!lastByIngredient.TryGetValue(movement.IngredientId, out current) ||
                    movement.CreatedAt >= current.CreatedAt)
                {
                    lastByIngredient[movement.IngredientId] = movement;
                }
            }

            var mismatched = new List<string>();
            foreach (var ingredient in ingredients)
            {
                StockMovement last;
                if (!lastByIngredient.TryGetValue(ingredient.Id, out last))
                {
                    continue;
                }

                if (last.StockAfter != ingredient.Stock)
                {
                    mismatched.Add(ingredient.Name + " (остаток " + FormatQuantity(ingredient.Stock) +
                                   ", по истории " + FormatQuantity(last.StockAfter) + ")");
                }
            }

            if (mismatched.Count > 0)
            {
                issues.Add(Format(
                    "Остаток не совпадает с последней операцией: " + mismatched.Count + " — ",
                    mismatched));
            }
        }

        private static string FormatQuantity(decimal value)
        {
            // Числа в замечаниях не зависят от текущей культуры: проверка — диагностика,
            // а не пользовательский ввод.
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Format(string prefix, IEnumerable<string> names)
        {
            var distinct = names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (distinct.Count == 0)
            {
                return prefix.TrimEnd(' ', '—') + ".";
            }

            var listed = string.Join(", ", distinct.Take(MaxListedItems));
            return prefix + listed + (distinct.Count > MaxListedItems ? " и др." : "") + ".";
        }
    }
}
