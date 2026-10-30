using System;
using System.Collections.Generic;
using CenterCoffee.Domain.Orders;

namespace CenterCoffee.Domain.Repositories
{
    public interface IOrderRepository : IRepository<Order>
    {
        IReadOnlyList<Order> GetByPeriod(DateTime fromInclusive, DateTime toInclusive);

        int CountCreatedOn(DateTime date);
    }
}
