using System;
using System.Globalization;
using CenterCoffee.Domain.Warehouse;
using CenterCoffee.WPF.Common;

namespace CenterCoffee.WPF.ViewModels
{
    /// <summary>
    /// Строка истории складских операций. Отделённая от домена модель представления,
    /// чтобы XAML не занимался форматированием бизнес-данных.
    /// </summary>
    public sealed class MovementRowViewModel
    {
        private readonly StockMovement _movement;

        public MovementRowViewModel(StockMovement movement)
        {
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        public DateTime CreatedAt => _movement.CreatedAt;

        public bool IsReceipt => _movement.Type == StockMovementType.Receipt;

        public bool IsWriteOff => _movement.Type == StockMovementType.WriteOff;

        public bool IsInitial => _movement.Type == StockMovementType.Initial;

        public string TypeText
        {
            get
            {
                switch (_movement.Type)
                {
                    case StockMovementType.Receipt:
                        return "Приход";
                    case StockMovementType.WriteOff:
                        return "Списание";
                    default:
                        return "Начальный остаток";
                }
            }
        }

        public string AmountText
        {
            get
            {
                var sign = IsReceipt ? "+" : IsWriteOff ? "−" : string.Empty;
                return sign + InputParsing.FormatQuantity(_movement.Amount) + " " +
                       MeasurementUnits.GetShortName(_movement.Unit);
            }
        }

        public string CreatedAtText
        {
            get { return _movement.CreatedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture); }
        }

        public string ReasonText
        {
            get
            {
                return string.IsNullOrWhiteSpace(_movement.Reason)
                    ? "Без комментария"
                    : _movement.Reason;
            }
        }

        public string StockAfterText
        {
            get
            {
                return "Остаток после: " + InputParsing.FormatQuantity(_movement.StockAfter) + " " +
                       MeasurementUnits.GetShortName(_movement.Unit);
            }
        }

        public string AuthorText
        {
            get
            {
                return string.IsNullOrWhiteSpace(_movement.CreatedBy)
                    ? string.Empty
                    : "Операция: " + _movement.CreatedBy;
            }
        }
    }
}
