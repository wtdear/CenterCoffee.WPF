using CenterCoffee.WPF.Mvvm;

namespace CenterCoffee.WPF.ViewModels
{
    public sealed class NavigationItemViewModel
    {
        public NavigationItemViewModel(string title, string icon, ViewModelBase page, bool isInDevelopment)
        {
            Title = title;
            Icon = icon;
            Page = page;
            IsInDevelopment = isInDevelopment;
        }

        public string Title { get; private set; }

        public string Icon { get; private set; }

        public ViewModelBase Page { get; private set; }

        public bool IsInDevelopment { get; private set; }
    }
}
