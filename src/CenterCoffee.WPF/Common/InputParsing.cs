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

        /// <summary>
        /// Проверяет имя новой папки: без разделителей пути и запрещённых символов,
        /// без точек на концах, чтобы Windows не создавал папку с другим именем.
        /// </summary>
        public static bool TryValidateFolderName(string name, out string error)
        {
            var normalized = (name ?? string.Empty).Trim();

            if (normalized.Length == 0)
            {
                error = "Введите имя новой папки.";
                return false;
            }

            if (normalized.Length > 120)
            {
                error = "Имя папки слишком длинное.";
                return false;
            }

            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                if (normalized.IndexOf(invalid) >= 0)
                {
                    error = "Имя папки содержит запрещённые символы.";
                    return false;
                }
            }

            if (normalized.EndsWith(" ", System.StringComparison.Ordinal) ||
                normalized.EndsWith(".", System.StringComparison.Ordinal))
            {
                error = "Имя папки не должно заканчиваться точкой или пробелом.";
                return false;
            }

            if (normalized == "." || normalized == "..")
            {
                error = "Такое имя папки недоступно.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
