using System;

namespace CenterCoffee.WPF.Services
{
    /// <summary>
    /// Уведомление экранов о том, что локальные данные были перечитаны, заменены или
    /// стали недоступны (например, восстановлением из резервной копии). Экраны,
    /// показывающие списки, должны перечитать их: иначе отрисовка и команды работали бы
    /// со ссылками на устаревшие объекты.
    /// </summary>
    public static class DataChangedNotifier
    {
        public static event EventHandler DataReplaced;

        /// <summary>
        /// Возникает, когда файл данных уже заменён, но приложение не смогло перечитать его.
        /// До перезапуска запись запрещена, чтобы не затереть восстановленные данные.
        /// </summary>
        public static event EventHandler DataBlocked;

        public static bool IsBlocked { get; private set; }

        public static void RaiseDataReplaced()
        {
            IsBlocked = false;
            var handler = DataReplaced;
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        public static void RaiseDataBlocked()
        {
            IsBlocked = true;
            var handler = DataBlocked;
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }
    }
}
