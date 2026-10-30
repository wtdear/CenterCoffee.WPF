using System;
using System.Windows.Input;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Common;
using CenterCoffee.Domain.Warehouse;
using CenterCoffee.WPF.Common;
using CenterCoffee.WPF.Mvvm;

namespace CenterCoffee.WPF.ViewModels.Dialogs
{
    public enum StockOperationKind
    {
        Receipt = 1,
        WriteOff = 2
    }

    public sealed class StockOperationViewModel : DialogViewModel
    {
        private readonly IWarehouseService _warehouse;
        private readonly Ingredient _ingredient;
        private readonly StockOperationKind _kind;

        private string _amountText;
        private string _reason;
        private string _errorMessage;

        public StockOperationViewModel(
            IWarehouseService warehouse,
            Ingredient ingredient,
            StockOperationKind kind)
        {
            _warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
            _ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
            _kind = kind;

            IsReceipt = kind == StockOperationKind.Receipt;
            Title = IsReceipt ? "Приход на склад" : "Списание со склада";
            Subtitle = IsReceipt
                ? "Добавьте поступившее количество"
                : "Спишите израсходованное количество";

            AmountText = string.Empty;
            Reason = string.Empty;

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(() => Close(false));
        }

        public bool IsReceipt { get; private set; }

        public bool IsWriteOff
        {
            get { return !IsReceipt; }
        }

        public string IngredientName
        {
            get { return _ingredient.Name; }
        }

        public string UnitText
        {
            get { return MeasurementUnits.GetShortName(_ingredient.Unit); }
        }

        public string CurrentStockText
        {
            get
            {
                return InputParsing.FormatQuantity(_ingredient.Stock) + " " + UnitText;
            }
        }

        public string SaveButtonText
        {
            get { return IsReceipt ? "Оприходовать" : "Списать"; }
        }

        public string MinStockText
        {
            get
            {
                return InputParsing.FormatQuantity(_ingredient.MinStock) + " " + UnitText;
            }
        }

        public string AmountText
        {
            get { return _amountText; }
            set
            {
                if (Set(ref _amountText, value))
                {
                    OnPropertyChanged(nameof(PreviewText));
                    OnPropertyChanged(nameof(PreviewIsNegative));
                }
            }
        }

        public string Reason
        {
            get { return _reason; }
            set { Set(ref _reason, value); }
        }

        public string PreviewText
        {
            get
            {
                decimal amount;
                if (!InputParsing.TryDecimal(AmountText, out amount) || amount <= 0m)
                {
                    return "Введите количество, чтобы увидеть результат.";
                }

                var result = IsReceipt ? _ingredient.Stock + amount : _ingredient.Stock - amount;
                return "Остаток после операции: " + InputParsing.FormatQuantity(result) + " " + UnitText;
            }
        }

        public bool PreviewIsNegative
        {
            get
            {
                decimal amount;
                if (!InputParsing.TryDecimal(AmountText, out amount) || amount <= 0m)
                {
                    return false;
                }

                return !IsReceipt && amount > _ingredient.Stock;
            }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            private set
            {
                if (Set(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError
        {
            get { return !string.IsNullOrWhiteSpace(_errorMessage); }
        }

        public ICommand SaveCommand { get; private set; }

        public ICommand CancelCommand { get; private set; }

        private void Save()
        {
            ErrorMessage = null;

            decimal amount;
            if (!InputParsing.TryDecimal(AmountText, out amount) || amount <= 0m)
            {
                ErrorMessage = "Введите количество больше нуля.";
                return;
            }

            if (IsWriteOff && amount > _ingredient.Stock)
            {
                ErrorMessage = "Нельзя списать больше, чем есть на складе. Доступно: " + CurrentStockText + ".";
                return;
            }

            try
            {
                if (IsReceipt)
                {
                    _warehouse.ReceiveStock(_ingredient.Id, amount, Reason);
                }
                else
                {
                    _warehouse.WriteOffStock(_ingredient.Id, amount, Reason);
                }

                Close(true);
            }
            catch (DomainException exception)
            {
                ErrorMessage = exception.Message;
            }
            catch (Exception exception)
            {
                ErrorMessage = "Не удалось выполнить операцию. " + exception.Message;
            }
        }
    }
}
