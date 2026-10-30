using System;
using System.Runtime.Serialization;

namespace CenterCoffee.Domain.Common
{
    /// <summary>
    /// Базовая сущность. Идентичность определяется типом и значением <see cref="Id"/>.
    /// </summary>
    [DataContract]
    public abstract class Entity : IEquatable<Entity>
    {
        [DataMember(Name = "id")]
        private Guid _id;

        protected Entity()
        {
            _id = Guid.NewGuid();
        }

        protected Entity(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Идентификатор сущности не может быть пустым.", nameof(id));
            }

            _id = id;
        }

        public Guid Id
        {
            get { return _id; }
        }

        public bool Equals(Entity other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return GetType() == other.GetType() && _id.Equals(other._id);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Entity);
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }
    }
}
