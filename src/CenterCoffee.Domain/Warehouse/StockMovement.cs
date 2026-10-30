using System;
using System.Runtime.Serialization;
using CenterCoffee.Domain.Common;

namespace CenterCoffee.Domain.Warehouse
{
    /// <summary>
    /// Запись истории движения по складу. Хранит снимок названия и единицы измерения,
    /// чтобы история оставалась читаемой при переименовании позиции.
    /// </summary>
    [DataContract]
    public sealed class StockMovement : Entity
    {
        [DataMember] private Guid _ingredientId;
        [DataMember] private string _ingredientName;
        [DataMember] private MeasurementUnit _unit;
        [DataMember] private StockMovementType _type;
        [DataMember] private decimal _amount;
        [DataMember] private decimal _stockAfter;
        [DataMember] private string _reason;
        [DataMember] private string _createdBy;
        [DataMember] private DateTime _createdAt;

        private StockMovement()
        {
        }

        public StockMovement(
            Guid ingredientId,
            string ingredientName,
            MeasurementUnit unit,
            StockMovementType type,
            decimal amount,
            decimal stockAfter,
            string reason = null,
            string createdBy = null,
            DateTime? createdAt = null)
        {
            _ingredientId = Guard.NotEmpty(ingredientId, "Ингредиент");
            _ingredientName = Guard.MaxLength(ingredientName, 80, "Название ингредиента");
            _unit = unit;
            _type = type;
            _amount = Guard.Positive(amount, "Количество");
            _stockAfter = Guard.NotNegative(stockAfter, "Остаток после операции");
            _reason = (reason ?? string.Empty).Trim();
            _createdBy = (createdBy ?? string.Empty).Trim();
            _createdAt = createdAt ?? DateTime.Now;
        }

        public Guid IngredientId => _ingredientId;

        public string IngredientName => _ingredientName;

        public MeasurementUnit Unit => _unit;

        public StockMovementType Type => _type;

        public decimal Amount => _amount;

        public decimal StockAfter => _stockAfter;

        public string Reason => _reason;

        public string CreatedBy => _createdBy;

        public DateTime CreatedAt => _createdAt;
    }
}
