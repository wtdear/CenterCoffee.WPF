using System;

namespace CenterCoffee.WPF.Mvvm
{
    public abstract class ViewModelBase : ObservableObject
    {
        private string _title = string.Empty;
        private string _subtitle = string.Empty;

        public string Title
        {
            get { return _title; }
            protected set { Set(ref _title, value); }
        }

        public string Subtitle
        {
            get { return _subtitle; }
            protected set { Set(ref _subtitle, value); }
        }

        public virtual void OnActivated()
        {
        }
    }
}
