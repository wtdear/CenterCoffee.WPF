using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using CenterCoffee.Domain.Common;
using CenterCoffee.Domain.Menu;

namespace CenterCoffee.Domain.Orders
{
    /// <summary>
    /// Позиция заказа. Хранит снимок названия и цены товара на момент продажи.
    /// </summary>
    [DataContract]
    public sealed class OrderItem
    {
        [DataMember] private Guid _productId;
        [DataMember] private string _productName;
        [DataMember] private string _icon;
        [DataMember] private decimal _unitPrice;
        [DataMember] private int _quantity;
        [DataMember] private List<OrderRecipeItem> _recipe = new List<OrderRecipeItem>();

        private OrderItem()
        {
        }

        internal OrderItem(Product product, int quantity, IEnumerable<OrderRecipeItem> recipe)
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            _productId = product.Id;
            _productName = product.Name;
            _icon = product.Icon;
            _unitPrice = product.Price;
            _quantity = ValidateQuantity(quantity);
            _recipe = recipe == null ? new List<OrderRecipeItem>() : recipe.ToList();
        }

        public Guid ProductId => _productId;

        public string ProductName => _productName;

        public string Icon => _icon;

        public decimal UnitPrice => _unitPrice;

        public int Quantity => _quantity;

        public decimal Total => _unitPrice * _quantity;

        public IReadOnlyList<OrderRecipeItem> Recipe =>
            (_recipe ?? (_recipe = new List<OrderRecipeItem>())).AsReadOnly();

        internal void IncreaseQuantity(int quantity)
        {
            _quantity += ValidateQuantity(quantity);
        }

        private static int ValidateQuantity(int quantity)
        {
            if (quantity <= 0)
            {
                throw new DomainException("Количество в позиции заказа должно быть больше нуля.");
            }

            return quantity;
        }
    }
}
