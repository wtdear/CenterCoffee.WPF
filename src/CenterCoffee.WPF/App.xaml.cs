using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using CenterCoffee.Application.Services;
using CenterCoffee.Infrastructure.Persistence;
using CenterCoffee.WPF.Services;
using CenterCoffee.WPF.ViewModels;

namespace CenterCoffee.WPF
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                ConfigureCulture();
                DispatcherUnhandledException += OnDispatcherUnhandledException;

                var dataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CenterCoffee");

                var dataFilePath = Environment.GetEnvironmentVariable("CENTERCOFFEE_DATA_PATH");
                if (string.IsNullOrWhiteSpace(dataFilePath))
                {
                    dataFilePath = Path.Combine(dataDirectory, "data.json");
                }

                var dataStore = new JsonDataStore(dataFilePath);
                var unitOfWork = new JsonUnitOfWork(dataStore);
                var warehouseService = new WarehouseService(unitOfWork);
                var dialogService = new DialogService();

                var mainViewModel = new MainViewModel(warehouseService, dialogService);
                var window = new MainWindow
                {
                    DataContext = mainViewModel
                };

                MainWindow = window;
                window.Show();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Не удалось запустить приложение.\n\n" + exception.Message,
                    "Центр кофе",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(-1);
            }
        }

        private static void ConfigureCulture()
        {
            var culture = CultureInfo.GetCultureInfo("ru-RU");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
        }

        private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "Произошла непредвиденная ошибка.\n\n" + e.Exception.Message,
                "Центр кофе",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }
    }
}
