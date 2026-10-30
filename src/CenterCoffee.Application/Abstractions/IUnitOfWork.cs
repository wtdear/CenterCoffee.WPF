using CenterCoffee.Domain.Repositories;

namespace CenterCoffee.Application.Abstractions
{
    /// <summary>
    /// Единица работы: даёт доступ к репозиториям и сохраняет изменения атомарно.
    /// Пока реализован только складской контур, остальные репозитории появятся
    /// по мере вывода модулей из стадии разработки.
    /// </summary>
    public interface IUnitOfWork
    {
        IIngredientRepository Ingredients { get; }

        IStockMovementRepository Movements { get; }

        void SaveChanges();
    }
}
