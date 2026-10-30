using System;
using System.Runtime.Serialization;
using CenterCoffee.Domain.Common;

namespace CenterCoffee.Domain.Warehouse
{
    /// <summary>
    /// Складская позиция (сырьё, расходники). Остаток изменяется только методами сущности.
    /// </summary>
    [DataContract]
    public sealed class Ingredient : Entity
    {
        [DataMember] private string _name;
        [DataMember] private MeasurementUnit _unit;
        [DataMember] private decimal _stock;
        [DataMember] private decimal _minStock;
        [DataMember] private decimal _purchasePrice;

        private Ingredient()
        {
        }

        public Ingredient(
            string name,
            MeasurementUnit unit,
            decimal initialStock,
            decimal minStock,
            decimal purchasePrice)
        {
            _name = Guard.MaxLength(name, 80, "Название ингредиента");
            _unit = unit;
            _stock = Guard.NotNegative(initialStock, "Начальный остаток");
            _minStock = Guard.NotNegative(minStock, "Минимальный остаток");
            _purchasePrice = Guard.NotNegative(purchasePrice, "Закупочная цена");
        }

        public string Name => _name;

        public MeasurementUnit Unit => _unit;

        public decimal Stock => _stock;

        public decimal MinStock => _minStock;

        public decimal PurchasePrice => _purchasePrice;

        public decimal StockValue => _stock * _purchasePrice;

        public bool IsLowStock => _stock <= _minStock;

        public void Rename(string name)
        {
            _name = Guard.MaxLength(name, 80, "Название ингредиента");
        }

        public void ChangeUnit(MeasurementUnit unit)
        {
            _unit = unit;
        }

        public void ChangeMinStock(decimal minStock)
        {
            _minStock = Guard.NotNegative(minStock, "Минимальный остаток");
        }

        public void ChangePurchasePrice(decimal purchasePrice)
        {
            _purchasePrice = Guard.NotNegative(purchasePrice, "Закупочная цена");
        }

        public void Receive(decimal amount)
        {
            _stock += Guard.Positive(amount, "Количество прихода");
        }

        public void WriteOff(decimal amount)
        {
            Guard.Positive(amount, "Количество списания");

            if (amount > _stock)
            {
                throw new DomainException(
                    "Недостаточно «" + _name + "» на складе. Доступно: " +
                    _stock.ToString("0.###") + ", требуется: " + amount.ToString("0.###") + ".");
            }

            _stock -= amount;
        }
    }
}
