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

                // Состояние файла проверяем до запуска экранов: повреждённая или несовместимая
                // база не должна выглядеть как пустой склад и не должна перезаписываться.
                var startupInspection = dataStore.Inspect();
                if (startupInspection.State == DataFileState.UnsupportedVersion ||
                    startupInspection.State == DataFileState.Corrupted)
                {
                    MessageBox.Show(
                        startupInspection.State == DataFileState.UnsupportedVersion
                            ? "Файл данных создан более новой версией программы (версия формата " +
                              startupInspection.SchemaVersion + ", поддерживается " + DataSchema.Version + ").\n\n" +
                              "Установите актуальную версию программы. Файл не изменён.\n\n" + dataFilePath
                            : "Локальный файл данных повреждён или имеет неверный формат.\n\n" +
                              startupInspection.ErrorMessage + "\n\nПрограмма не будет изменять этот файл. " +
                              "Восстановите данные из резервной копии, заменив файл вручную.\n\n" + dataFilePath,
                        "Центр кофе",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    Shutdown(-1);
                    return;
                }

                var unitOfWork = new JsonUnitOfWork(dataStore);
                var warehouseService = new WarehouseService(unitOfWork);
                var maintenanceService = new JsonDataMaintenanceService(dataStore, unitOfWork.Reload);
                var dialogService = new DialogService();

                var mainViewModel = new MainViewModel(warehouseService, maintenanceService, dialogService);
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
