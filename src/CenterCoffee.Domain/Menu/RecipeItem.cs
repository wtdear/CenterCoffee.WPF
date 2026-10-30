using System.Runtime.Serialization;
using CenterCoffee.Domain.Common;

namespace CenterCoffee.Domain.Menu
{
    /// <summary>
    /// Строка рецептуры: сколько ингредиента уходит на одну порцию.
    /// </summary>
    [DataContract]
    public sealed class RecipeItem
    {
        [DataMember] private System.Guid _ingredientId;
        [DataMember] private decimal _amount;

        private RecipeItem()
        {
        }

        public RecipeItem(System.Guid ingredientId, decimal amount)
        {
            _ingredientId = Guard.NotEmpty(ingredientId, "Ингредиент");
            _amount = Guard.Positive(amount, "Количество ингредиента");
        }

        public System.Guid IngredientId => _ingredientId;

        public decimal Amount => _amount;
    }
}
