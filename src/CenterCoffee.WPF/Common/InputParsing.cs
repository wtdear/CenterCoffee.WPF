using System.Globalization;

namespace CenterCoffee.WPF.Common
{
    internal static class InputParsing
    {
        public static bool TryDecimal(string text, out decimal value)
        {
            var normalized = (text ?? string.Empty).Trim();

            if (normalized.Length == 0)
            {
                value = 0m;
                return true;
            }

            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
                   || decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        public static string FormatQuantity(decimal value)
        {
            return value.ToString("#,##0.###", CultureInfo.CurrentCulture);
        }
    }
}
