using CenterCoffee.Domain.Employees;

namespace CenterCoffee.Domain.Repositories
{
    public interface IEmployeeRepository : IRepository<Employee>
    {
        Employee FindByLogin(string login);
    }
}
