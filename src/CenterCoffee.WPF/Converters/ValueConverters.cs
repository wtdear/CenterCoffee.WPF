using System;
using System.Globalization;
using System.Windows.Data;
using CenterCoffee.Domain.Warehouse;
using CenterCoffee.WPF.Common;

namespace CenterCoffee.WPF.Converters
{
    public sealed class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is decimal))
            {
                return "—";
            }

            var format = parameter as string ?? "N0";
            var amount = (decimal)value;
            return amount.ToString(format, CultureInfo.GetCultureInfo("ru-RU")) + " ₽";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class QuantityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is decimal))
            {
                return "—";
            }

            return ((decimal)value).ToString("0.###", CultureInfo.GetCultureInfo("ru-RU"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class UnitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is MeasurementUnit
                ? MeasurementUnits.GetShortName((MeasurementUnit)value)
                : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
