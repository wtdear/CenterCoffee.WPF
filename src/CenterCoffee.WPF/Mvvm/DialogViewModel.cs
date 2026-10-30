using System;

namespace CenterCoffee.WPF.Mvvm
{
    /// <summary>
    /// База для моделей представления модальных окон: сообщает окну,
    /// когда его нужно закрыть и с каким результатом.
    /// </summary>
    public abstract class DialogViewModel : ViewModelBase
    {
        public event Action<bool> CloseRequested;

        protected void Close(bool saved)
        {
            var handler = CloseRequested;
            if (handler != null)
            {
                handler(saved);
            }
        }
    }
}
