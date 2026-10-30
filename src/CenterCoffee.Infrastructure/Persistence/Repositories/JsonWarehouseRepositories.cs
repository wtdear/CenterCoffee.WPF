using System;
using System.Collections.Generic;
using System.Linq;
using CenterCoffee.Domain.Repositories;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Infrastructure.Persistence.Repositories
{
    internal sealed class JsonIngredientRepository : JsonRepository<Ingredient>, IIngredientRepository
    {
        public JsonIngredientRepository(List<Ingredient> items) : base(items)
        {
        }
    }

    internal sealed class JsonStockMovementRepository : JsonRepository<StockMovement>, IStockMovementRepository
    {
        public JsonStockMovementRepository(List<StockMovement> items) : base(items)
        {
        }

        public IReadOnlyList<StockMovement> GetRecent(int limit)
        {
            return Items
                .OrderByDescending(movement => movement.CreatedAt)
                .Take(limit)
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<StockMovement> GetRecent(Guid ingredientId, int limit)
        {
            return Items
                .Where(movement => movement.IngredientId == ingredientId)
                .OrderByDescending(movement => movement.CreatedAt)
                .Take(limit)
                .ToList()
                .AsReadOnly();
        }
    }
}
