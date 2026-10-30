using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using CenterCoffee.Domain.Common;

namespace CenterCoffee.Domain.Menu
{
    [DataContract]
    public sealed class Product : Entity
    {
        [DataMember] private Guid _categoryId;
        [DataMember] private string _name;
        [DataMember] private decimal _price;
        [DataMember] private string _description;
        [DataMember] private string _icon;
        [DataMember] private bool _isActive;
        [DataMember] private List<RecipeItem> _recipe = new List<RecipeItem>();

        private Product()
        {
        }

        public Product(
            string name,
            Guid categoryId,
            decimal price,
            string description = null,
            string icon = "☕")
        {
            _name = Guard.MaxLength(name, 80, "Название товара");
            _categoryId = Guard.NotEmpty(categoryId, "Категория");
            _price = Guard.Positive(price, "Цена");
            _description = (description ?? string.Empty).Trim();
            _icon = string.IsNullOrWhiteSpace(icon) ? "☕" : icon.Trim();
            _isActive = true;
            _recipe = new List<RecipeItem>();
        }

        public Guid CategoryId => _categoryId;

        public string Name => _name;

        public decimal Price => _price;

        public string Description => _description;

        public string Icon => _icon;

        public bool IsActive => _isActive;

        public IReadOnlyList<RecipeItem> Recipe =>
            (_recipe ?? (_recipe = new List<RecipeItem>())).AsReadOnly();

        public void Rename(string name)
        {
            _name = Guard.MaxLength(name, 80, "Название товара");
        }

        public void ChangeCategory(Guid categoryId)
        {
            _categoryId = Guard.NotEmpty(categoryId, "Категория");
        }

        public void ChangePrice(decimal price)
        {
            _price = Guard.Positive(price, "Цена");
        }

        public void ChangeDescription(string description)
        {
            _description = (description ?? string.Empty).Trim();
        }

        public void ChangeIcon(string icon)
        {
            _icon = string.IsNullOrWhiteSpace(icon) ? "☕" : icon.Trim();
        }

        public void SetRecipe(IEnumerable<RecipeItem> recipe)
        {
            _recipe = recipe == null ? new List<RecipeItem>() : recipe.ToList();
        }

        public void Activate()
        {
            _isActive = true;
        }

        public void Deactivate()
        {
            _isActive = false;
        }
    }
}
