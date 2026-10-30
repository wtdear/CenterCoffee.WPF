using System;
using System.Globalization;
using System.Windows.Media;
using CenterCoffee.Domain.Warehouse;
using CenterCoffee.WPF.Common;

namespace CenterCoffee.WPF.ViewModels
{
    /// <summary>
    /// Строка таблицы склада. Доменная сущность не зависит от WPF,
    /// а все подписи и цвета для интерфейса готовятся здесь.
    /// </summary>
    public sealed class IngredientRowViewModel
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        public IngredientRowViewModel(Ingredient ingredient)
        {
            Ingredient = ingredient ?? throw new ArgumentNullException(nameof(ingredient));
        }

        public Ingredient Ingredient { get; private set; }

        public Guid Id
        {
            get { return Ingredient.Id; }
        }

        public string Name
        {
            get { return Ingredient.Name; }
        }

        public string UnitText
        {
            get { return MeasurementUnits.GetShortName(Ingredient.Unit); }
        }

        public decimal Stock
        {
            get { return Ingredient.Stock; }
        }

        public string StockText
        {
            get { return Ingredient.Stock.ToString("#,##0.###", RussianCulture); }
        }

        public string MinStockText
        {
            get { return Ingredient.MinStock.ToString("#,##0.###", RussianCulture); }
        }

        public decimal PurchasePrice
        {
            get { return Ingredient.PurchasePrice; }
        }

        public string PurchasePriceText
        {
            get { return Ingredient.PurchasePrice.ToString("N2", RussianCulture) + " ₽"; }
        }

        public string StockValueText
        {
            get { return Ingredient.StockValue.ToString("N0", RussianCulture) + " ₽"; }
        }

        public bool IsOutOfStock
        {
            get { return Ingredient.Stock <= 0m; }
        }

        public bool IsLowStock
        {
            get { return Ingredient.IsLowStock; }
        }

        public string StatusText
        {
            get
            {
                if (IsOutOfStock)
                {
                    return "Нет в наличии";
                }

                return IsLowStock ? "Ниже минимума" : "В норме";
            }
        }

        public Brush StatusBrush
        {
            get
            {
                if (IsOutOfStock)
                {
                    return StatusPalette.Danger;
                }

                return IsLowStock ? StatusPalette.Warning : StatusPalette.Success;
            }
        }
    }
}
