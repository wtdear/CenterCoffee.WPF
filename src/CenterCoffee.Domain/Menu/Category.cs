using System.Runtime.Serialization;
using CenterCoffee.Domain.Common;

namespace CenterCoffee.Domain.Menu
{
    [DataContract]
    public sealed class Category : Entity
    {
        [DataMember] private string _name;
        [DataMember] private int _sortOrder;
        [DataMember] private bool _isActive;

        private Category()
        {
        }

        public Category(string name, int sortOrder = 0)
        {
            _name = Guard.MaxLength(name, 60, "Название категории");
            _sortOrder = sortOrder;
            _isActive = true;
        }

        public string Name => _name;

        public int SortOrder => _sortOrder;

        public bool IsActive => _isActive;

        public void Rename(string name)
        {
            _name = Guard.MaxLength(name, 60, "Название категории");
        }

        public void Move(int sortOrder)
        {
            _sortOrder = sortOrder;
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
