using System.Windows.Media;

namespace CenterCoffee.WPF.Common
{
    public static class StatusPalette
    {
        public static readonly Brush Success = Create(0x3F, 0x7D, 0x58);
        public static readonly Brush Warning = Create(0xB7, 0x79, 0x1F);
        public static readonly Brush Danger = Create(0xB5, 0x45, 0x3C);
        public static readonly Brush Muted = Create(0x8A, 0x7A, 0x6D);

        private static Brush Create(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
