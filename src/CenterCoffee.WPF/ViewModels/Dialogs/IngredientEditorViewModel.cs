using System;
using System.Globalization;
using System.Windows.Input;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Common;
using CenterCoffee.Domain.Warehouse;
using CenterCoffee.WPF.Common;
using CenterCoffee.WPF.Mvvm;

namespace CenterCoffee.WPF.ViewModels.Dialogs
{
    public sealed class IngredientEditorViewModel : DialogViewModel
    {
        private readonly IWarehouseService _warehouse;
        private readonly Ingredient _ingredient;

        private string _name;
        private MeasurementUnit _selectedUnit;
        private string _initialStockText;
        private string _minStockText;
        private string _purchasePriceText;
        private string _errorMessage;

        public IngredientEditorViewModel(IWarehouseService warehouse, Ingredient ingredient = null)
        {
            _warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
            _ingredient = ingredient;

            Units = new[]
            {
                MeasurementUnit.Gram,
                MeasurementUnit.Milliliter,
                MeasurementUnit.Piece
            };

            IsNew = ingredient == null;
            Title = IsNew ? "Новая складская позиция" : "Редактирование позиции";
            Subtitle = IsNew
                ? "Добавьте сырьё или расходный материал"
                : "Измените параметры складской позиции";

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(() => Close(false));

            if (IsNew)
            {
                _selectedUnit = MeasurementUnit.Gram;
                _initialStockText = "0";
                _minStockText = "0";
                _purchasePriceText = "0";
            }
            else
            {
                _name = ingredient.Name;
                _selectedUnit = ingredient.Unit;
                _initialStockText = InputParsing.FormatQuantity(ingredient.Stock);
                _minStockText = InputParsing.FormatQuantity(ingredient.MinStock);
                _purchasePriceText = ingredient.PurchasePrice.ToString("0.##", CultureInfo.CurrentCulture);
            }
        }

        public bool IsNew { get; private set; }

        public MeasurementUnit[] Units { get; private set; }

        public bool ShowUnitHint
        {
            get { return !IsNew && _ingredient.Stock > 0m; }
        }

        public Ingredient SavedIngredient { get; private set; }

        public string Name
        {
            get { return _name; }
            set { Set(ref _name, value); }
        }

        public MeasurementUnit SelectedUnit
        {
            get { return _selectedUnit; }
            set { Set(ref _selectedUnit, value); }
        }

        public string InitialStockText
        {
            get { return _initialStockText; }
            set { Set(ref _initialStockText, value); }
        }

        public string MinStockText
        {
            get { return _minStockText; }
            set { Set(ref _minStockText, value); }
        }

        public string PurchasePriceText
        {
            get { return _purchasePriceText; }
            set { Set(ref _purchasePriceText, value); }
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

            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "Укажите название складской позиции.";
                return;
            }

            decimal initialStock = 0m;
            decimal minStock;
            decimal purchasePrice;

            if ((IsNew && !InputParsing.TryDecimal(InitialStockText, out initialStock)) ||
                !InputParsing.TryDecimal(MinStockText, out minStock) ||
                !InputParsing.TryDecimal(PurchasePriceText, out purchasePrice))
            {
                ErrorMessage = "Проверьте числовые значения: используйте цифры и разделитель дробной части.";
                return;
            }

            if (initialStock < 0m || minStock < 0m || purchasePrice < 0m)
            {
                ErrorMessage = "Остатки и цена не могут быть отрицательными.";
                return;
            }

            try
            {
                if (IsNew)
                {
                    SavedIngredient = _warehouse.CreateIngredient(
                        Name,
                        SelectedUnit,
                        initialStock,
                        minStock,
                        purchasePrice);
                }
                else
                {
                    _warehouse.UpdateIngredient(
                        _ingredient.Id,
                        Name,
                        SelectedUnit,
                        minStock,
                        purchasePrice);
                    SavedIngredient = _ingredient;
                }

                Close(true);
            }
            catch (DomainException exception)
            {
                ErrorMessage = exception.Message;
            }
            catch (Exception exception)
            {
                ErrorMessage = "Не удалось сохранить позицию. " + exception.Message;
            }
        }
    }
}
