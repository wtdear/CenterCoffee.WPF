using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Warehouse;
using CenterCoffee.WPF.Mvvm;
using CenterCoffee.WPF.Services;
using CenterCoffee.WPF.ViewModels.Dialogs;

namespace CenterCoffee.WPF.ViewModels
{
    public sealed class WarehouseViewModel : ViewModelBase
    {
        private readonly IWarehouseService _warehouse;
        private readonly IDialogService _dialogs;

        private string _searchText = string.Empty;
        private bool _lowStockOnly;
        private IngredientRowViewModel _selectedIngredient;
        private WarehouseSummary _summary = new WarehouseSummary(0, 0, 0, 0);
        private bool _showAllMovements;
        private bool _hasMovements;

        public WarehouseViewModel(IWarehouseService warehouse, IDialogService dialogs)
        {
            _warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

            Ingredients = new ObservableCollection<IngredientRowViewModel>();
            Movements = new ObservableCollection<MovementRowViewModel>();

            RefreshCommand = new RelayCommand(Refresh);
            AddIngredientCommand = new RelayCommand(AddIngredient);
            EditIngredientCommand = new RelayCommand(EditIngredient, () => HasSelection);
            ReceiveStockCommand = new RelayCommand(ReceiveStock, () => HasSelection);
            WriteOffStockCommand = new RelayCommand(WriteOffStock, () => HasSelection);

            Title = "Склад";
            Subtitle = "Остатки, приход и списание";
        }

        public ObservableCollection<IngredientRowViewModel> Ingredients { get; private set; }

        public ObservableCollection<MovementRowViewModel> Movements { get; private set; }

        public WarehouseSummary Summary
        {
            get { return _summary; }
            private set { Set(ref _summary, value); }
        }

        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (Set(ref _searchText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(EmptyStateText));
                    ReloadIngredients();
                }
            }
        }

        public bool LowStockOnly
        {
            get { return _lowStockOnly; }
            set
            {
                if (Set(ref _lowStockOnly, value))
                {
                    OnPropertyChanged(nameof(EmptyStateText));
                    ReloadIngredients();
                }
            }
        }

        public IngredientRowViewModel SelectedIngredient
        {
            get { return _selectedIngredient; }
            set
            {
                if (Set(ref _selectedIngredient, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(HistoryCaption));
                    RaiseSelectionCommands();
                    LoadMovements();
                }
            }
        }

        public bool HasSelection
        {
            get { return _selectedIngredient != null; }
        }

        public bool IsEmpty
        {
            get { return Ingredients.Count == 0; }
        }

        public string EmptyStateText
        {
            get
            {
                if (LowStockOnly || !string.IsNullOrWhiteSpace(SearchText))
                {
                    return "По заданным условиям ничего не найдено.";
                }

                return "На складе пока нет позиций. Добавьте первую позицию, чтобы начать учёт.";
            }
        }

        public bool ShowAllMovements
        {
            get { return _showAllMovements; }
            set
            {
                if (Set(ref _showAllMovements, value))
                {
                    OnPropertyChanged(nameof(HistoryCaption));
                    LoadMovements();
                }
            }
        }

        public bool HasMovements
        {
            get { return _hasMovements; }
            private set { Set(ref _hasMovements, value); }
        }

        public string HistoryCaption
        {
            get
            {
                if (ShowAllMovements)
                {
                    return "Все операции";
                }

                return HasSelection ? "История по позиции" : "История операций";
            }
        }

        public ICommand RefreshCommand { get; private set; }

        public ICommand AddIngredientCommand { get; private set; }

        public ICommand EditIngredientCommand { get; private set; }

        public ICommand ReceiveStockCommand { get; private set; }

        public ICommand WriteOffStockCommand { get; private set; }

        public override void OnActivated()
        {
            Refresh();
        }

        private void Refresh()
        {
            ReloadIngredients();
        }

        private void ReloadIngredients(Guid? preferredIngredientId = null)
        {
            try
            {
                var selectedId = preferredIngredientId ??
                                 (_selectedIngredient == null
                                     ? (Guid?)null
                                     : _selectedIngredient.Id);

                var items = _warehouse.GetIngredients(SearchText, LowStockOnly);

                Ingredients.Clear();
                foreach (var ingredient in items)
                {
                    Ingredients.Add(new IngredientRowViewModel(ingredient));
                }

                Summary = _warehouse.GetSummary();
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(EmptyStateText));

                var selection = selectedId.HasValue
                    ? Ingredients.FirstOrDefault(item => item.Id == selectedId.Value)
                    : null;

                SelectedIngredient = selection ?? Ingredients.FirstOrDefault();

                OnPropertyChanged(nameof(HasSelection));
                RaiseSelectionCommands();
                LoadMovements();
            }
            catch (Exception exception)
            {
                _dialogs.ShowError("Не удалось загрузить данные склада. " + exception.Message);
            }
        }

        private void LoadMovements()
        {
            Movements.Clear();

            if (!ShowAllMovements && _selectedIngredient == null)
            {
                HasMovements = false;
                OnPropertyChanged(nameof(HistoryCaption));
                return;
            }

            try
            {
                var ingredientId = ShowAllMovements
                    ? (Guid?)null
                    : _selectedIngredient.Id;

                var movements = _warehouse.GetMovements(ingredientId, 100);
                foreach (var movement in movements)
                {
                    Movements.Add(new MovementRowViewModel(movement));
                }

                HasMovements = Movements.Count > 0;
            }
            catch (Exception exception)
            {
                _dialogs.ShowError("Не удалось загрузить историю операций. " + exception.Message);
            }

            OnPropertyChanged(nameof(HistoryCaption));
        }

        private void AddIngredient()
        {
            var editor = new IngredientEditorViewModel(_warehouse);
            if (_dialogs.ShowIngredientEditor(editor))
            {
                ReloadIngredients(editor.SavedIngredient == null ? (Guid?)null : editor.SavedIngredient.Id);
            }
        }

        private void EditIngredient()
        {
            var selected = _selectedIngredient;
            if (selected == null)
            {
                return;
            }

            var editor = new IngredientEditorViewModel(_warehouse, selected.Ingredient);
            if (_dialogs.ShowIngredientEditor(editor))
            {
                ReloadIngredients(selected.Id);
            }
        }

        private void ReceiveStock()
        {
            var selected = _selectedIngredient;
            if (selected == null)
            {
                return;
            }

            var operation = new StockOperationViewModel(
                _warehouse,
                selected.Ingredient,
                StockOperationKind.Receipt);

            if (_dialogs.ShowStockOperation(operation))
            {
                ReloadIngredients(selected.Id);
            }
        }

        private void WriteOffStock()
        {
            var selected = _selectedIngredient;
            if (selected == null)
            {
                return;
            }

            var operation = new StockOperationViewModel(
                _warehouse,
                selected.Ingredient,
                StockOperationKind.WriteOff);

            if (_dialogs.ShowStockOperation(operation))
            {
                ReloadIngredients(selected.Id);
            }
        }

        private void RaiseSelectionCommands()
        {
            ((RelayCommand)EditIngredientCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ReceiveStockCommand).RaiseCanExecuteChanged();
            ((RelayCommand)WriteOffStockCommand).RaiseCanExecuteChanged();
        }
    }
}
