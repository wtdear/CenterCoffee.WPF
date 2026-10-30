using System;

namespace CenterCoffee.Domain.Common
{
    internal static class Guard
    {
        public static string NotEmpty(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new DomainException("Поле «" + fieldName + "» обязательно для заполнения.");
            }

            return value.Trim();
        }

        public static string MaxLength(string value, int maxLength, string fieldName)
        {
            var result = NotEmpty(value, fieldName);
            if (result.Length > maxLength)
            {
                throw new DomainException("Поле «" + fieldName + "» не должно быть длиннее " + maxLength + " символов.");
            }

            return result;
        }

        public static decimal Positive(decimal value, string fieldName)
        {
            if (value <= 0m)
            {
                throw new DomainException("Поле «" + fieldName + "» должно быть больше нуля.");
            }

            return value;
        }

        public static decimal NotNegative(decimal value, string fieldName)
        {
            if (value < 0m)
            {
                throw new DomainException("Поле «" + fieldName + "» не может быть отрицательным.");
            }

            return value;
        }

        public static int NotNegative(int value, string fieldName)
        {
            if (value < 0)
            {
                throw new DomainException("Поле «" + fieldName + "» не может быть отрицательным.");
            }

            return value;
        }

        public static Guid NotEmpty(Guid value, string fieldName)
        {
            if (value == Guid.Empty)
            {
                throw new DomainException("Поле «" + fieldName + "» обязательно для заполнения.");
            }

            return value;
        }

        public static DateTime NotDefault(DateTime value, string fieldName)
        {
            if (value == default(DateTime))
            {
                throw new DomainException("Поле «" + fieldName + "» обязательно для заполнения.");
            }

            return value;
        }
    }
}
