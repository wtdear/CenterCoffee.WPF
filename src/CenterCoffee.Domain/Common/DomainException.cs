using System;

namespace CenterCoffee.Domain.Common
{
    /// <summary>
    /// Исключение бизнес-правила. UI показывает его текст пользователю без стектрейса.
    /// </summary>
    [Serializable]
    public sealed class DomainException : Exception
    {
        public DomainException(string message) : base(message)
        {
        }

        public DomainException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
