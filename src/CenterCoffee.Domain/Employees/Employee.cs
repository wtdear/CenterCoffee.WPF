using System;
using System.Runtime.Serialization;
using CenterCoffee.Domain.Common;

namespace CenterCoffee.Domain.Employees
{
    /// <summary>
    /// Сотрудник кофейни. Пароль хранится только в виде хэша и соли.
    /// </summary>
    [DataContract]
    public sealed class Employee : Entity
    {
        [DataMember] private string _fullName;
        [DataMember] private string _login;
        [DataMember] private string _passwordHash;
        [DataMember] private string _passwordSalt;
        [DataMember] private EmployeeRole _role;
        [DataMember] private string _phone;
        [DataMember] private bool _isActive;
        [DataMember] private DateTime _createdAt;

        private Employee()
        {
        }

        public Employee(
            string fullName,
            string login,
            string passwordHash,
            string passwordSalt,
            EmployeeRole role,
            string phone = null)
        {
            _fullName = Guard.MaxLength(fullName, 100, "ФИО");
            _login = NormalizeLogin(login);
            _passwordHash = Guard.NotEmpty(passwordHash, "Хэш пароля");
            _passwordSalt = Guard.NotEmpty(passwordSalt, "Соль пароля");
            _role = role;
            _phone = (phone ?? string.Empty).Trim();
            _isActive = true;
            _createdAt = DateTime.Now;
        }

        public string FullName => _fullName;

        public string Login => _login;

        public string PasswordHash => _passwordHash;

        public string PasswordSalt => _passwordSalt;

        public EmployeeRole Role => _role;

        public string Phone => _phone;

        public bool IsActive => _isActive;

        public DateTime CreatedAt => _createdAt;

        public bool IsAdministrator => _role == EmployeeRole.Administrator;

        public string Initials
        {
            get
            {
                var parts = (_fullName ?? string.Empty)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0)
                {
                    return "?";
                }

                if (parts.Length == 1)
                {
                    return parts[0].Substring(0, 1).ToUpperInvariant();
                }

                return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
            }
        }

        public void Rename(string fullName)
        {
            _fullName = Guard.MaxLength(fullName, 100, "ФИО");
        }

        public void SetPhone(string phone)
        {
            _phone = (phone ?? string.Empty).Trim();
        }

        public void ChangeRole(EmployeeRole role)
        {
            _role = role;
        }

        public void SetPassword(string passwordHash, string passwordSalt)
        {
            _passwordHash = Guard.NotEmpty(passwordHash, "Хэш пароля");
            _passwordSalt = Guard.NotEmpty(passwordSalt, "Соль пароля");
        }

        public void Activate()
        {
            _isActive = true;
        }

        public void Deactivate()
        {
            _isActive = false;
        }

        private static string NormalizeLogin(string login)
        {
            var value = Guard.MaxLength(login, 50, "Логин").ToLowerInvariant();

            if (value.Contains(" "))
            {
                throw new DomainException("Логин не должен содержать пробелы.");
            }

            if (value.Length < 3)
            {
                throw new DomainException("Логин должен содержать не менее 3 символов.");
            }

            return value;
        }
    }
}
