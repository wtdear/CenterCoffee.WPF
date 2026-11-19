using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CenterCoffee.WPF.Converters
{
    /// <summary>
    /// Возвращает true, когда все переданные строки непусты и совпадают.
    /// Удобен для подсветки выбранного элемента: не требует отдельного флага в VM
    /// и не срабатывает, пока сравнение не заполнено (например, период ещё не выбран).
    /// </summary>
    public sealed class AllEqualConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return false;
            }

            string first = null;
            foreach (var value in values)
            {
                var text = value as string;
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }

                if (first == null)
                {
                    first = text;
                }
                else if (!string.Equals(first, text, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool && (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value != Visibility.Visible;
        }
    }
}
