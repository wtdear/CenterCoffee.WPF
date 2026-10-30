using System;
using System.Collections.Generic;
using System.Linq;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Common;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Application.Services
{
    /// <summary>
    /// Бизнес-логика складского контура: остатки, приход, списание и история операций.
    /// Не знает ничего о WPF и способе хранения данных.
    /// </summary>
    public sealed class WarehouseService : IWarehouseService
    {
        private const string DefaultOperator = "Панель управления";

        private readonly IUnitOfWork _unitOfWork;

        public WarehouseService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public IReadOnlyList<Ingredient> GetIngredients(string searchText = null, bool lowStockOnly = false)
        {
            IEnumerable<Ingredient> query = _unitOfWork.Ingredients.GetAll();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var search = searchText.Trim();
                query = query.Where(item =>
                    item.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (lowStockOnly)
            {
                query = query.Where(item => item.IsLowStock);
            }

            return query
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
                .AsReadOnly();
        }

        public WarehouseSummary GetSummary()
        {
            var ingredients = _unitOfWork.Ingredients.GetAll();

            return new WarehouseSummary(
                ingredients.Count,
                ingredients.Count(item => item.IsLowStock && item.Stock > 0m),
                ingredients.Count(item => item.Stock <= 0m),
                ingredients.Sum(item => item.Stock * item.PurchasePrice));
        }

        public Ingredient GetIngredient(Guid id)
        {
            var ingredient = _unitOfWork.Ingredients.Find(id);
            if (ingredient == null)
            {
                throw new DomainException("Складская позиция не найдена.");
            }

            return ingredient;
        }

        public Ingredient CreateIngredient(
            string name,
            MeasurementUnit unit,
            decimal initialStock,
            decimal minStock,
            decimal purchasePrice)
        {
            EnsureNameIsUnique(name, null);

            var ingredient = new Ingredient(name, unit, initialStock, minStock, purchasePrice);
            _unitOfWork.Ingredients.Add(ingredient);

            if (initialStock > 0m)
            {
                _unitOfWork.Movements.Add(new StockMovement(
                    ingredient.Id,
                    ingredient.Name,
                    ingredient.Unit,
                    StockMovementType.Initial,
                    initialStock,
                    ingredient.Stock,
                    "Начальный остаток",
                    DefaultOperator));
            }

            _unitOfWork.SaveChanges();
            return ingredient;
        }

        public void UpdateIngredient(
            Guid id,
            string name,
            MeasurementUnit unit,
            decimal minStock,
            decimal purchasePrice)
        {
            var ingredient = GetIngredient(id);
            EnsureNameIsUnique(name, ingredient.Id);

            if (ingredient.Unit != unit && ingredient.Stock > 0m)
            {
                throw new DomainException(
                    "Нельзя изменить единицу измерения, пока остаток не равен нулю. " +
                    "Сначала списание или корректировка остатка.");
            }

            ingredient.Rename(name);
            ingredient.ChangeUnit(unit);
            ingredient.ChangeMinStock(minStock);
            ingredient.ChangePurchasePrice(purchasePrice);
            _unitOfWork.SaveChanges();
        }

        public void ReceiveStock(Guid ingredientId, decimal amount, string reason)
        {
            var ingredient = GetIngredient(ingredientId);
            ingredient.Receive(amount);

            _unitOfWork.Movements.Add(new StockMovement(
                ingredient.Id,
                ingredient.Name,
                ingredient.Unit,
                StockMovementType.Receipt,
                amount,
                ingredient.Stock,
                NormalizeReason(reason, "Приход на склад"),
                DefaultOperator));

            _unitOfWork.SaveChanges();
        }

        public void WriteOffStock(Guid ingredientId, decimal amount, string reason)
        {
            var ingredient = GetIngredient(ingredientId);
            ingredient.WriteOff(amount);

            _unitOfWork.Movements.Add(new StockMovement(
                ingredient.Id,
                ingredient.Name,
                ingredient.Unit,
                StockMovementType.WriteOff,
                amount,
                ingredient.Stock,
                NormalizeReason(reason, "Списание со склада"),
                DefaultOperator));

            _unitOfWork.SaveChanges();
        }

        public IReadOnlyList<StockMovement> GetMovements(Guid? ingredientId, int limit = 100)
        {
            var safeLimit = Math.Max(1, Math.Min(limit, 500));

            return ingredientId.HasValue
                ? _unitOfWork.Movements.GetRecent(ingredientId.Value, safeLimit)
                : _unitOfWork.Movements.GetRecent(safeLimit);
        }

        private void EnsureNameIsUnique(string name, Guid? exceptId)
        {
            var normalized = (name ?? string.Empty).Trim();

            if (normalized.Length == 0)
            {
                throw new DomainException("Название складской позиции обязательно для заполнения.");
            }

            var duplicate = _unitOfWork.Ingredients.GetAll().FirstOrDefault(item =>
                (!exceptId.HasValue || item.Id != exceptId.Value) &&
                string.Equals(item.Name, normalized, StringComparison.CurrentCultureIgnoreCase));

            if (duplicate != null)
            {
                throw new DomainException("Позиция со сходным названием уже есть на складе.");
            }
        }

        private static string NormalizeReason(string reason, string defaultValue)
        {
            var normalized = (reason ?? string.Empty).Trim();
            return normalized.Length == 0 ? defaultValue : normalized;
        }
    }
}
