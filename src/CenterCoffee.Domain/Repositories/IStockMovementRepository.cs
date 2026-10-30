using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Domain.Repositories
{
    public interface IStockMovementRepository : IRepository<StockMovement>
    {
        System.Collections.Generic.IReadOnlyList<StockMovement> GetRecent(int limit);

        System.Collections.Generic.IReadOnlyList<StockMovement> GetRecent(
            System.Guid ingredientId,
            int limit);
    }
}
