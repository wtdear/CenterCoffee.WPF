using System.Collections.Generic;
using CenterCoffee.WPF.Mvvm;

namespace CenterCoffee.WPF.ViewModels
{
    /// <summary>
    /// Заглушка для модулей, которые ещё не реализованы. Позволяет показать
    /// заказчику план развития без имитации несуществующей бизнес-логики.
    /// </summary>
    public sealed class ComingSoonViewModel : ViewModelBase
    {
        public ComingSoonViewModel(
            string title,
            string icon,
            string description,
            IReadOnlyList<string> plannedFeatures)
        {
            Title = title;
            Subtitle = "Модуль находится в разработке";
            Icon = icon;
            Description = description;
            PlannedFeatures = plannedFeatures;
        }

        public string Icon { get; private set; }

        public string Description { get; private set; }

        public IReadOnlyList<string> PlannedFeatures { get; private set; }
    }
}
