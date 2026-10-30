using System;
using System.Collections.Generic;
using CenterCoffee.Domain.Common;

namespace CenterCoffee.Domain.Repositories
{
    /// <summary>
    /// Базовый контракт хранилища сущности. Реализация скрыта в слое Infrastructure.
    /// </summary>
    public interface IRepository<TEntity> where TEntity : Entity
    {
        IReadOnlyList<TEntity> GetAll();

        TEntity Find(Guid id);

        void Add(TEntity entity);

        void Remove(TEntity entity);
    }
}
