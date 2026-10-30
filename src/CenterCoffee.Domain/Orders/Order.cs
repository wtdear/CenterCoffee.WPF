using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using CenterCoffee.Domain.Common;
using CenterCoffee.Domain.Menu;

namespace CenterCoffee.Domain.Orders
{
    /// <summary>
    /// Заказ кофейни. После создания сразу находится в статусе «Оплачен»,
    /// затем может быть выдан или отменён. Отмена возвращает ингредиенты на склад.
    /// </summary>
    [DataContract]
    public sealed class Order : Entity
    {
        [DataMember] private string _number;
        [DataMember] private DateTime _createdAt;
        [DataMember] private Guid _employeeId;
        [DataMember] private string _employeeName;
        [DataMember] private OrderType _type;
        [DataMember] private PaymentMethod _paymentMethod;
        [DataMember] private OrderStatus _status;
        [DataMember] private decimal _discountPercent;
        [DataMember] private string _comment;
        [DataMember] private DateTime? _closedAt;
        [DataMember] private List<OrderItem> _items = new List<OrderItem>();

        private Order()
        {
        }

        public Order(
            string number,
            Guid employeeId,
            string employeeName,
            OrderType type,
            PaymentMethod paymentMethod,
            decimal discountPercent = 0m,
            string comment = null,
            DateTime? createdAt = null)
        {
            _number = Guard.MaxLength(number, 40, "Номер заказа");
            _employeeId = Guard.NotEmpty(employeeId, "Сотрудник");
            _employeeName = Guard.MaxLength(employeeName, 100, "Сотрудник");
            _type = type;
            _paymentMethod = paymentMethod;
            _status = OrderStatus.Paid;
            _discountPercent = ValidateDiscount(discountPercent);
            _comment = (comment ?? string.Empty).Trim();
            _createdAt = createdAt ?? DateTime.Now;
            _items = new List<OrderItem>();
        }

        public string Number => _number;

        public DateTime CreatedAt => _createdAt;

        public Guid EmployeeId => _employeeId;

        public string EmployeeName => _employeeName;

        public OrderType Type => _type;

        public PaymentMethod PaymentMethod => _paymentMethod;

        public OrderStatus Status => _status;

        public decimal DiscountPercent => _discountPercent;

        public string Comment => _comment;

        public DateTime? ClosedAt => _closedAt;

        public IReadOnlyList<OrderItem> Items =>
            (_items ?? (_items = new List<OrderItem>())).AsReadOnly();

        public int ItemsCount => Items.Sum(item => item.Quantity);

        public decimal Subtotal => Items.Sum(item => item.Total);

        public decimal DiscountAmount =>
            Math.Round(Subtotal * _discountPercent / 100m, 2, MidpointRounding.AwayFromZero);

        public decimal Total => Subtotal - DiscountAmount;

        public bool CanBeCompleted => _status == OrderStatus.Paid;

        public bool CanBeCancelled => _status == OrderStatus.Paid;

        public void AddItem(Product product, int quantity, IEnumerable<OrderRecipeItem> recipeSnapshot)
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            var existing = Items.FirstOrDefault(item => item.ProductId == product.Id);
            if (existing != null)
            {
                existing.IncreaseQuantity(quantity);
                return;
            }

            _items.Add(new OrderItem(product, quantity, recipeSnapshot));
        }

        public void Complete()
        {
            if (_status != OrderStatus.Paid)
            {
                throw new DomainException("Выдать можно только оплаченный заказ.");
            }

            _status = OrderStatus.Completed;
            _closedAt = DateTime.Now;
        }

        public void Cancel()
        {
            if (_status != OrderStatus.Paid)
            {
                throw new DomainException("Отменить можно только оплаченный заказ.");
            }

            _status = OrderStatus.Cancelled;
            _closedAt = DateTime.Now;
        }

        private static decimal ValidateDiscount(decimal discountPercent)
        {
            if (discountPercent < 0m || discountPercent > 100m)
            {
                throw new DomainException("Скидка должна быть в диапазоне от 0 до 100%.");
            }

            return discountPercent;
        }
    }
}
