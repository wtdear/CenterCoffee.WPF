using System;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Repositories;
using CenterCoffee.Infrastructure.Persistence.Repositories;

namespace CenterCoffee.Infrastructure.Persistence
{
    public sealed class JsonUnitOfWork : IUnitOfWork
    {
        private readonly JsonDataStore _store;

        private AppData _data;
        private IIngredientRepository _ingredients;
        private IStockMovementRepository _movements;
        private bool _isReady;

        public JsonUnitOfWork(JsonDataStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Load();
        }

        public bool IsReady => _isReady;

        public IIngredientRepository Ingredients
        {
            get
            {
                EnsureReady();
                return _ingredients;
            }
        }

        public IStockMovementRepository Movements
        {
            get
            {
                EnsureReady();
                return _movements;
            }
        }

        public void SaveChanges()
        {
            EnsureReady();
            _store.Save(_data);
        }

        public void Reload()
        {
            Load();
        }

        private void Load()
        {
            // Сначала сбрасываем готовность: если чтение упадёт, единица работы не должна
            // продолжать отдавать репозитории со старыми данными, а сохранение должно быть запрещено.
            _isReady = false;

            _data = _store.Load();
            _data.EnsureCollections();

            _ingredients = new JsonIngredientRepository(_data.Ingredients);
            _movements = new JsonStockMovementRepository(_data.Movements);
            _isReady = true;
        }

        private void EnsureReady()
        {
            if (!_isReady)
            {
                throw new InvalidOperationException(
                    "Данные склада не загружены из хранилища. Перезапустите программу или повторите " +
                    "проверку в разделе «Целостность».");
            }
        }
    }
}
