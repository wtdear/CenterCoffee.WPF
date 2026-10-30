using System;
using System.Collections.Generic;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Application.Abstractions
{
    public interface IWarehouseService
    {
        IReadOnlyList<Ingredient> GetIngredients(string searchText = null, bool lowStockOnly = false);

        WarehouseSummary GetSummary();

        Ingredient GetIngredient(Guid id);

        Ingredient CreateIngredient(
            string name,
            MeasurementUnit unit,
            decimal initialStock,
            decimal minStock,
            decimal purchasePrice);

        void UpdateIngredient(
            Guid id,
            string name,
            MeasurementUnit unit,
            decimal minStock,
            decimal purchasePrice);

        void ReceiveStock(Guid ingredientId, decimal amount, string reason);

        void WriteOffStock(Guid ingredientId, decimal amount, string reason);

        IReadOnlyList<StockMovement> GetMovements(Guid? ingredientId, int limit = 100);
    }

    public sealed class WarehouseSummary
    {
        public WarehouseSummary(int positionsCount, int lowStockCount, int outOfStockCount, decimal stockValue)
        {
            PositionsCount = positionsCount;
            LowStockCount = lowStockCount;
            OutOfStockCount = outOfStockCount;
            StockValue = stockValue;
        }

        public int PositionsCount { get; private set; }

        public int LowStockCount { get; private set; }

        public int OutOfStockCount { get; private set; }

        public decimal StockValue { get; private set; }
    }
}
