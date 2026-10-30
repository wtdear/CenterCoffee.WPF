using System;
using CenterCoffee.Domain.Warehouse;

namespace CenterCoffee.WPF.Common
{
    public static class MeasurementUnits
    {
        public static string GetShortName(MeasurementUnit unit)
        {
            switch (unit)
            {
                case MeasurementUnit.Gram:
                    return "г";
                case MeasurementUnit.Milliliter:
                    return "мл";
                case MeasurementUnit.Piece:
                    return "шт";
                default:
                    return string.Empty;
            }
        }
    }
}
