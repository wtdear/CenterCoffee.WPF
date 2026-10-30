using System;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Repositories;
using CenterCoffee.Infrastructure.Persistence.Repositories;

namespace CenterCoffee.Infrastructure.Persistence
{
    public sealed class JsonUnitOfWork : IUnitOfWork
    {
        private readonly JsonDataStore _store;
        private readonly AppData _data;

        public JsonUnitOfWork(JsonDataStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _data = store.Load();
            _data.EnsureCollections();

            Ingredients = new JsonIngredientRepository(_data.Ingredients);
            Movements = new JsonStockMovementRepository(_data.Movements);
        }

        public IIngredientRepository Ingredients { get; private set; }

        public IStockMovementRepository Movements { get; private set; }

        public void SaveChanges()
        {
            _store.Save(_data);
        }
    }
}
