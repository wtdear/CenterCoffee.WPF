using System;
using System.Runtime.Serialization;
using CenterCoffee.Domain.Common;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.Domain.Orders
{
    /// <summary>
    /// Снимок расхода ингредиента на момент продажи. Нужен, чтобы корректно
    /// вернуть продукты на склад при отмене заказа, даже если рецепт позже изменился.
    /// </summary>
    [DataContract]
    public sealed class OrderRecipeItem
    {
        [DataMember] private Guid _ingredientId;
        [DataMember] private string _ingredientName;
        [DataMember] private MeasurementUnit _unit;
        [DataMember] private decimal _amountPerUnit;

        private OrderRecipeItem()
        {
        }

        public OrderRecipeItem(Guid ingredientId, string ingredientName, MeasurementUnit unit, decimal amountPerUnit)
        {
            _ingredientId = Guard.NotEmpty(ingredientId, "Ингредиент");
            _ingredientName = Guard.NotEmpty(ingredientName, "Название ингредиента");
            _unit = unit;
            _amountPerUnit = Guard.Positive(amountPerUnit, "Количество ингредиента");
        }

        public Guid IngredientId => _ingredientId;

        public string IngredientName => _ingredientName;

        public MeasurementUnit Unit => _unit;

        public decimal AmountPerUnit => _amountPerUnit;

        public decimal AmountFor(int quantity)
        {
            return _amountPerUnit * quantity;
        }
    }
}
