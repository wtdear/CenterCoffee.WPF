using System;
using System.Collections.Generic;
using System.Linq;
using CenterCoffee.Domain.Common;
using CenterCoffee.Domain.Repositories;

namespace CenterCoffee.Infrastructure.Persistence.Repositories
{
    public abstract class JsonRepository<TEntity> : IRepository<TEntity>
        where TEntity : Entity
    {
        private readonly List<TEntity> _items;

        protected JsonRepository(List<TEntity> items)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        protected List<TEntity> Items => _items;

        public IReadOnlyList<TEntity> GetAll()
        {
            return _items.ToList().AsReadOnly();
        }

        public TEntity Find(Guid id)
        {
            return _items.FirstOrDefault(item => item.Id == id);
        }

        public void Add(TEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (_items.Any(item => item.Id == entity.Id))
            {
                throw new InvalidOperationException("Сущность с таким идентификатором уже сохранена.");
            }

            _items.Add(entity);
        }

        public void Remove(TEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _items.Remove(entity);
        }
    }
}
