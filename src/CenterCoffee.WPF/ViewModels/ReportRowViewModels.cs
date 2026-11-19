using System;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.WPF.Common;

namespace CenterCoffee.WPF.ViewModels
{
    /// <summary>
    /// Строка отчёта об оборотах. Все подписи и форматы готовятся здесь,
    /// чтобы XAML не выполнял расчёты и не форматировал бизнес-данные.
    /// </summary>
    public sealed class TurnoverRowViewModel
    {
        private readonly TurnoverItem _item;

        public TurnoverRowViewModel(TurnoverItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
        }

        public string Name
        {
            get { return _item.Name; }
        }

        public string UnitText
        {
            get { return MeasurementUnits.GetShortName(_item.Unit); }
        }

        public decimal OpeningStock
        {
            get { return _item.OpeningStock; }
        }

        public decimal Received
        {
            get { return _item.Received; }
        }

        public decimal WrittenOff
        {
            get { return _item.WrittenOff; }
        }

        public decimal ClosingStock
        {
            get { return _item.ClosingStock; }
        }

        public string OpeningStockText
        {
            get { return InputParsing.FormatQuantity(_item.OpeningStock); }
        }

        public string ReceivedText
        {
            get { return "+" + InputParsing.FormatQuantity(_item.Received); }
        }

        public string WrittenOffText
        {
            get { return "−" + InputParsing.FormatQuantity(_item.WrittenOff); }
        }

        public string ClosingStockText
        {
            get { return InputParsing.FormatQuantity(_item.ClosingStock); }
        }
    }

    /// <summary>
    /// Строка отчёта о дефиците: сколько докупить до минимального остатка.
    /// </summary>
    public sealed class DeficitRowViewModel
    {
        private readonly DeficitItem _item;

        public DeficitRowViewModel(DeficitItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
        }

        public string Name
        {
            get { return _item.Ingredient.Name; }
        }

        public string UnitText
        {
            get { return MeasurementUnits.GetShortName(_item.Ingredient.Unit); }
        }

        public decimal RecommendedAmount
        {
            get { return _item.RecommendedAmount; }
        }

        public string StockText
        {
            get { return InputParsing.FormatQuantity(_item.Ingredient.Stock); }
        }

        public string MinStockText
        {
            get { return InputParsing.FormatQuantity(_item.Ingredient.MinStock); }
        }

        public string RecommendedAmountText
        {
            get { return InputParsing.FormatQuantity(_item.RecommendedAmount); }
        }

        public decimal PurchaseCost
        {
            get { return _item.RecommendedAmount * _item.Ingredient.PurchasePrice; }
        }

        public bool IsOutOfStock
        {
            get { return _item.Ingredient.Stock <= 0m; }
        }

        public string StatusText
        {
            get { return IsOutOfStock ? "Нет в наличии" : "Ниже минимума"; }
        }

        public System.Windows.Media.Brush StatusBrush
        {
            get { return IsOutOfStock ? StatusPalette.Danger : StatusPalette.Warning; }
        }
    }
}
