using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.WPF.Mvvm;
using CenterCoffee.WPF.Services;

namespace CenterCoffee.WPF.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private NavigationItemViewModel _selectedItem;

        public MainViewModel(IWarehouseService warehouseService, IDialogService dialogService)
        {
            var warehousePage = new WarehouseViewModel(warehouseService, dialogService);

            NavigationItems = new ObservableCollection<NavigationItemViewModel>
            {
                new NavigationItemViewModel(
                    "Склад",
                    "📦",
                    warehousePage,
                    false),
                new NavigationItemViewModel(
                    "Продажи",
                    "🧾",
                    CreateComingSoon(
                        "Продажи",
                        "🧾",
                        "Рабочее место бариста: выбор напитков и десертов, оформление заказа и оплата.",
                        new[]
                        {
                            "Каталог позиций меню с категориями и ценами",
                            "Корзина заказа, количество, скидка и комментарий",
                            "Оплата наличными и картой, печать чека",
                            "Списание ингредиентов по рецептуре при продаже"
                        }),
                    true),
                new NavigationItemViewModel(
                    "Заказы",
                    "🗂",
                    CreateComingSoon(
                        "Заказы",
                        "🗂",
                        "Журнал заказов с фильтрами по периоду и статусу.",
                        new[]
                        {
                            "Список заказов и детализация позиций",
                            "Статусы: оплачен, выдан, отменён",
                            "Отмена с возвратом ингредиентов на склад",
                            "Повтор заказа и просмотр чека"
                        }),
                    true),
                new NavigationItemViewModel(
                    "Меню",
                    "☕",
                    CreateComingSoon(
                        "Меню",
                        "☕",
                        "Управление ассортиментом кофейни и рецептурами.",
                        new[]
                        {
                            "Категории и карточки товаров",
                            "Цены, описания и видимость позиций",
                            "Рецептуры для автоматического списания",
                            "Управление доступностью товара"
                        }),
                    true),
                new NavigationItemViewModel(
                    "Сотрудники",
                    "👥",
                    CreateComingSoon(
                        "Сотрудники",
                        "👥",
                        "Учёт сотрудников, ролей и доступа к разделам.",
                        new[]
                        {
                            "Справочник сотрудников",
                            "Роли: администратор и бариста",
                            "Авторизация и смена пароля",
                            "Журнал действий пользователей"
                        }),
                    true),
                new NavigationItemViewModel(
                    "Отчёты",
                    "📊",
                    CreateComingSoon(
                        "Отчёты",
                        "📊",
                        "Аналитика продаж и движения складских запасов.",
                        new[]
                        {
                            "Выручка и количество чеков за период",
                            "Средний чек и популярные позиции",
                            "Стоимость и движение запасов",
                            "Экспорт отчётов"
                        }),
                    true)
            };

            SelectedItem = NavigationItems[0];
            TodayText = Capitalize(DateTime.Now.ToString("dddd, d MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU")));
            StorageLabel = "Локальная база";
            VersionText = "v0.2 • склад";
        }

        public ObservableCollection<NavigationItemViewModel> NavigationItems { get; private set; }

        public NavigationItemViewModel SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (Set(ref _selectedItem, value))
                {
                    OnPropertyChanged(nameof(CurrentPage));
                    if (value != null && value.Page != null)
                    {
                        value.Page.OnActivated();
                    }
                }
            }
        }

        public ViewModelBase CurrentPage
        {
            get { return _selectedItem == null ? null : _selectedItem.Page; }
        }

        public string TodayText { get; private set; }

        public string StorageLabel { get; private set; }

        public string VersionText { get; private set; }

        private static ComingSoonViewModel CreateComingSoon(
            string title,
            string icon,
            string description,
            string[] plannedFeatures)
        {
            return new ComingSoonViewModel(title, icon, description, plannedFeatures);
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return char.ToUpper(value[0], CultureInfo.GetCultureInfo("ru-RU")) + value.Substring(1);
        }
    }
}
