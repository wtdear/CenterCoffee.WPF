using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CenterCoffee.Application.Abstractions;
using CenterCoffee.Domain.Common;
using CenterCoffee.WPF.Mvvm;
using CenterCoffee.WPF.Services;

namespace CenterCoffee.WPF.ViewModels
{
    /// <summary>
    /// Вкладка экрана отчётов. Размер периода хранится отдельно у каждой вкладки:
    /// остатки и дефицит показывают состояние на дату, а обороты и движения считаются
    /// за выбранный период.
    /// </summary>
    public abstract class WarehouseReportTabViewModel : ViewModelBase
    {
        private bool _hasSnapshot;
        private DateTime _reportFrom = DateTime.Today;
        private DateTime _reportTo = DateTime.Today;

        protected WarehouseReportTabViewModel(
            WarehouseReportKind kind,
            string title,
            string description,
            bool hasPeriod)
        {
            Kind = kind;
            Title = title;
            Description = description;
            HasPeriod = hasPeriod;
        }

        public WarehouseReportKind Kind { get; private set; }

        public string Description { get; private set; }

        /// <summary>Вкладка показывает показатели за период или состояние на дату.</summary>
        public bool HasPeriod { get; private set; }

        /// <summary>Сформирован ли отчёт на текущих входных данных.</summary>
        public bool HasSnapshot
        {
            get { return _hasSnapshot; }
            protected set { Set(ref _hasSnapshot, value); }
        }

        /// <summary>Начало периода; для отчётов на дату совпадает с датой формирования.</summary>
        public virtual DateTime PeriodFrom
        {
            get { return _reportFrom; }
            set
            {
                if (Set(ref _reportFrom, value.Date))
                {
                    OnPeriodChanged();
                }
            }
        }

        /// <summary>Конец периода; для отчётов на дату совпадает с датой формирования.</summary>
        public virtual DateTime PeriodTo
        {
            get { return _reportTo; }
            set
            {
                if (Set(ref _reportTo, value.Date))
                {
                    OnPeriodChanged();
                }
            }
        }

        /// <summary>Подпись активного пресета периода; null, если границы выбраны вручную.</summary>
        public virtual string ActivePeriodPresetTitle
        {
            get { return null; }
        }

        /// <summary>Загрузка выполняется ведущим экраном: вкладка не знает о хранилище.</summary>
        internal IWarehouseReportService ReportService { get; set; }

        /// <summary>Вызывается ведущим экраном при изменении дат активной вкладки.</summary>
        internal Action PeriodChangedCallback { get; set; }

        /// <summary>Только чтение данных, без обращения к UI, для выгрузки в файл.</summary>
        internal abstract object BuildSnapshot();

        /// <summary>Табличное представление отчёта, сформированного заново.</summary>
        internal abstract ReportTable BuildTable(IWarehouseReportExporter exporter);

        internal abstract void Apply(object snapshot);

        internal abstract void Clear();

        internal void Load()
        {
            if (ReportService == null)
            {
                return;
            }

            Apply(BuildSnapshot());
        }

        /// <summary>Реакция на изменение границ периода: подписи и признаки отчёта.</summary>
        protected virtual void OnPeriodChanged()
        {
            OnPropertyChanged(nameof(PeriodText));
            OnPropertyChanged(nameof(PeriodError));
            OnPropertyChanged(nameof(HasPeriodError));
            OnPropertyChanged(nameof(ActivePeriodPresetTitle));

            var callback = PeriodChangedCallback;
            if (callback != null)
            {
                callback();
            }
        }

        public string PeriodText
        {
            get
            {
                var culture = CultureInfo.GetCultureInfo("ru-RU");
                return _reportFrom.ToString("dd.MM.yyyy", culture) + " — " +
                       _reportTo.ToString("dd.MM.yyyy", culture);
            }
        }

        /// <summary>Сообщение о неверном периоде; пуст при корректных датах.</summary>
        public string PeriodError
        {
            get
            {
                return _reportFrom > _reportTo
                    ? "Начало периода не может быть позже конца периода."
                    : null;
            }
        }

        public bool HasPeriodError
        {
            get { return _reportFrom > _reportTo; }
        }
    }

    /// <summary>
    /// Экран отчётов. Вкладки только отображают готовые показатели: все расчёты
    /// выполняет сервис отчётов, а выгрузка — экспортёр отчётов и запись файла.
    /// </summary>
    public sealed class WarehouseReportsViewModel : ViewModelBase
    {
        private readonly IWarehouseReportService _reports;
        private readonly IWarehouseReportExporter _exporter;
        private readonly IReportExportWriter _writer;
        private readonly IDialogService _dialogs;

        private WarehouseReportTabViewModel _selectedTab;
        private bool _isBusy;
        private string _statusMessage;
        private bool _statusIsError;
        private string _blockedMessage;

        public WarehouseReportsViewModel(
            IWarehouseReportService reports,
            IWarehouseReportExporter exporter,
            IReportExportWriter writer,
            IDialogService dialogs)
        {
            _reports = reports ?? throw new ArgumentNullException(nameof(reports));
            _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

            var stockTab = new StockReportTabViewModel();
            var turnoverTab = new TurnoverReportTabViewModel();
            var deficitTab = new DeficitReportTabViewModel();
            var movementsTab = new MovementsReportTabViewModel();

            Tabs = new ObservableCollection<WarehouseReportTabViewModel>
            {
                stockTab,
                turnoverTab,
                deficitTab,
                movementsTab
            };

            foreach (var tab in Tabs)
            {
                tab.ReportService = _reports;
                var captured = tab;
                tab.PeriodChangedCallback = () => OnTabPeriodChanged(captured);
            }

            Title = "Отчёты";
            Subtitle = "Складские отчёты: остатки, обороты, дефицит и движение за период";

            RefreshCommand = new RelayCommand(Refresh, () => !IsBusy && !IsBlocked);
            ExportCommand = new RelayCommand(Export, () => !IsBusy && !IsBlocked && _selectedTab != null);

            DataChangedNotifier.DataReplaced += OnDataReplaced;
            DataChangedNotifier.DataBlocked += OnDataBlocked;

            SelectedTab = Tabs[0];
        }

        public ObservableCollection<WarehouseReportTabViewModel> Tabs { get; private set; }

        public WarehouseReportTabViewModel SelectedTab
        {
            get { return _selectedTab; }
            set
            {
                // Смена вкладки не должна сбрасывать выбранные даты, поэтому загружаем
                // отчёт только если он ещё ни разу не формировался для этой вкладки.
                if (!Set(ref _selectedTab, value))
                {
                    return;
                }

                if (value != null && !value.HasSnapshot)
                {
                    LoadTab(value);
                }

                ReloadCommands();
            }
        }

        public bool IsBlocked
        {
            get { return !string.IsNullOrEmpty(_blockedMessage); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                if (Set(ref _isBusy, value))
                {
                    ReloadCommands();
                }
            }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            private set
            {
                if (Set(ref _statusMessage, value))
                {
                    OnPropertyChanged(nameof(HasStatus));
                }
            }
        }

        public bool HasStatus
        {
            get { return !string.IsNullOrWhiteSpace(_statusMessage); }
        }

        public bool StatusIsError
        {
            get { return _statusIsError; }
            private set { Set(ref _statusIsError, value); }
        }

        public string BlockedMessage
        {
            get { return _blockedMessage; }
        }

        public ICommand RefreshCommand { get; private set; }

        public ICommand ExportCommand { get; private set; }

        public override void OnActivated()
        {
            if (IsBlocked)
            {
                return;
            }

            if (_selectedTab != null)
            {
                LoadTab(_selectedTab);
            }
        }

        private void OnDataReplaced(object sender, EventArgs e)
        {
            _blockedMessage = null;
            OnPropertyChanged(nameof(IsBlocked));
            OnPropertyChanged(nameof(BlockedMessage));
            ReloadCommands();

            var current = _selectedTab;
            if (current != null)
            {
                LoadTab(current);
            }
        }

        private void OnDataBlocked(object sender, EventArgs e)
        {
            _blockedMessage =
                "Данные были заменены восстановленной копией, но не перечитались. " +
                "Отчёты недоступны — закройте и снова откройте программу.";
            OnPropertyChanged(nameof(IsBlocked));
            OnPropertyChanged(nameof(BlockedMessage));

            foreach (var tab in Tabs)
            {
                tab.Clear();
            }

            ShowStatus(_blockedMessage, true);
            ReloadCommands();
        }

        private void Refresh()
        {
            if (_selectedTab == null)
            {
                return;
            }

            LoadTab(_selectedTab);
        }

        /// <summary>
        /// Изменение границ периода активной вкладки сразу пересобирает отчёт, чтобы
        /// пользователь видел результат выбора дат без отдельного нажатия.
        /// </summary>
        private void OnTabPeriodChanged(WarehouseReportTabViewModel tab)
        {
            if (tab == null || !ReferenceEquals(tab, _selectedTab))
            {
                return;
            }

            if (tab.HasPeriodError)
            {
                // Пока период некорректен, отчёт не пересчитываем: показываем подсказку.
                StatusIsError = true;
                StatusMessage = tab.PeriodError;
                return;
            }

            LoadTab(tab);
        }

        private void LoadTab(WarehouseReportTabViewModel tab)
        {
            if (IsBlocked || tab == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                tab.Load();
                StatusMessage = null;
            }
            catch (DomainException exception)
            {
                ShowStatus(exception.Message, true);
            }
            catch (Exception exception)
            {
                ShowStatus("Не удалось сформировать отчёт. " + exception.Message, true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Export()
        {
            var tab = _selectedTab;
            if (tab == null)
            {
                return;
            }

            try
            {
                // Отчёт формируется заново в момент выгрузки: файл должен содержать
                // актуальные данные, а не снимок экрана, если вкладка была открыта давно.
                tab.Load();
            }
            catch (DomainException exception)
            {
                ShowStatus(exception.Message, true);
                return;
            }
            catch (Exception exception)
            {
                ShowStatus("Не удалось сформировать отчёт. " + exception.Message, true);
                return;
            }

            var suggestedName = _exporter.GetSuggestedFileName(tab.Kind, tab.PeriodFrom, tab.PeriodTo);
            var targetPath = _dialogs.PickReportExportFile(suggestedName, GetDefaultExportDirectory());
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            // Диалог не добавляет расширение, если выбран фильтр «Все файлы»:
            // дописываем формат сами, иначе Excel откроет файл как неизвестный.
            targetPath = EnsureReportExtension(targetPath, _exporter.GetFileExtension());

            ReportTable table;
            try
            {
                table = tab.BuildTable(_exporter);
            }
            catch (DomainException exception)
            {
                ShowStatus(exception.Message, true);
                return;
            }
            catch (Exception exception)
            {
                ShowStatus("Не удалось сформировать отчёт. " + exception.Message, true);
                return;
            }

            string savedPath;
            IsBusy = true;
            try
            {
                savedPath = _writer.Write(table, targetPath);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is InvalidDataException ||
                exception is System.Xml.XmlException)
            {
                IsBusy = false;
                ShowStatus("Не удалось сохранить отчёт: " + exception.Message, true);
                return;
            }
            finally
            {
                IsBusy = false;
            }

            ShowStatus("Отчёт сохранён: " + savedPath, false);
        }

        private string GetDefaultExportDirectory()
        {
            try
            {
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return string.IsNullOrWhiteSpace(documents)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : documents;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is ArgumentException ||
                exception is System.Security.SecurityException)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Дописывает расширение отчёта, если пользователь не указал его при выборе
        /// фильтра «Все файлы». Сравнение без учёта регистра и без параметров
        /// расширения, чтобы не менять имя, уже содержащее нужный формат.
        /// </summary>
        private static string EnsureReportExtension(string path, string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return path;
            }

            var normalizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
                ? extension
                : "." + extension;

            if (path.EndsWith(normalizedExtension, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return path + normalizedExtension;
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusIsError = isError;
            StatusMessage = message;
        }

        private void ReloadCommands()
        {
            ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Периодная вкладка: выбор начала и конца периода. Границы хранятся в базовой
    /// вкладке, здесь задаётся только начальный период по умолчанию. Быстрые пресеты
    /// периода меняют те же даты, поэтому отчёт пересчитывается их общим механизмом.
    /// </summary>
    public abstract class PeriodReportTabViewModel : WarehouseReportTabViewModel
    {
        /// <summary>Начало истории для пресета «За всё время»; null, если истории нет.</summary>
        private DateTime? _allTimeStart;

        protected PeriodReportTabViewModel(
            WarehouseReportKind kind,
            string title,
            string description)
            : base(kind, title, description, true)
        {
            PeriodTo = DateTime.Today;
            PeriodFrom = PeriodTo.AddDays(-29);

            PeriodPresets = new ReadOnlyCollection<ReportPeriodPreset>(
                new List<ReportPeriodPreset>
                {
                    new ReportPeriodPreset(
                        "Сегодня",
                        () => SetPeriod(DateTime.Today, DateTime.Today),
                        (from, to) => from == DateTime.Today && to == DateTime.Today),
                    new ReportPeriodPreset(
                        "Вчера",
                        () =>
                        {
                            var yesterday = DateTime.Today.AddDays(-1);
                            SetPeriod(yesterday, yesterday);
                        },
                        (from, to) =>
                        {
                            var yesterday = DateTime.Today.AddDays(-1);
                            return from == yesterday && to == yesterday;
                        }),
                    new ReportPeriodPreset(
                        "Неделя",
                        () =>
                        {
                            var today = DateTime.Today;
                            SetPeriod(GetStartOfWeek(today), today);
                        },
                        (from, to) => from == GetStartOfWeek(DateTime.Today) && to == DateTime.Today),
                    new ReportPeriodPreset(
                        "Месяц",
                        () =>
                        {
                            var today = DateTime.Today;
                            SetPeriod(new DateTime(today.Year, today.Month, 1), today);
                        },
                        (from, to) =>
                        {
                            var today = DateTime.Today;
                            return from == new DateTime(today.Year, today.Month, 1) && to == today;
                        }),
                    new ReportPeriodPreset(
                        "Квартал",
                        () =>
                        {
                            var today = DateTime.Today;
                            SetPeriod(GetStartOfQuarter(today), today);
                        },
                        (from, to) => from == GetStartOfQuarter(DateTime.Today) && to == DateTime.Today),
                    new ReportPeriodPreset(
                        "За всё время",
                        ApplyAllTimePeriod,
                        (from, to) => to == DateTime.Today && _allTimeStart.HasValue && from == _allTimeStart.Value)
                });
        }

        /// <summary>Быстрые пресеты периода; «За всё время» учитывает историю склада.</summary>
        public IReadOnlyCollection<ReportPeriodPreset> PeriodPresets { get; private set; }

        /// <summary>Выбранная подпись периода для подсветки активного пресета.</summary>
        public override string ActivePeriodPresetTitle
        {
            get
            {
                foreach (var preset in PeriodPresets)
                {
                    if (preset.Matches(PeriodFrom, PeriodTo))
                    {
                        return preset.Title;
                    }
                }

                return null;
            }
        }

        private void SetPeriod(DateTime from, DateTime to)
        {
            // Сначала конец периода, затем начало: промежуточное состояние с
            // началом позже конца дало бы ложную ошибку периода во время пересчёта.
            PeriodTo = to;
            PeriodFrom = from;
            OnPropertyChanged(nameof(ActivePeriodPresetTitle));
        }

        private void ApplyAllTimePeriod()
        {
            if (ReportService == null)
            {
                return;
            }

            var earliest = ReportService.GetEarliestMovementDate();
            if (!earliest.HasValue)
            {
                // История пуста: показываем пустой отчёт за сегодня, а не выдуманную дату.
                _allTimeStart = null;
                SetPeriod(DateTime.Today, DateTime.Today);
                return;
            }

            _allTimeStart = earliest.Value;
            SetPeriod(earliest.Value, DateTime.Today);
        }

        /// <summary>Понедельник текущей недели.</summary>
        private static DateTime GetStartOfWeek(DateTime date)
        {
            return date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        }

        /// <summary>Первое число первого месяца текущего квартала.</summary>
        private static DateTime GetStartOfQuarter(DateTime date)
        {
            var quarterStartMonth = ((date.Month - 1) / 3) * 3 + 1;
            return new DateTime(date.Year, quarterStartMonth, 1);
        }
    }

    /// <summary>
    /// Пресет периода отчёта: подпись на кнопке, применение и проверка совпадения
    /// с текущими границами. Сравнение по датам нужно, чтобы подсветить активный
    /// пресет и не показывать его активным при ручном выборе границ.
    /// </summary>
    public sealed class ReportPeriodPreset : ICommand
    {
        private readonly Action _apply;
        private readonly Func<DateTime, DateTime, bool> _matches;

        public ReportPeriodPreset(
            string title,
            Action apply,
            Func<DateTime, DateTime, bool> matches)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));
            _matches = matches ?? throw new ArgumentNullException(nameof(matches));
        }

        public string Title { get; private set; }

        public void Apply()
        {
            _apply();
        }

        /// <summary>Совпадают ли границы периода с пресетом.</summary>
        public bool Matches(DateTime from, DateTime to)
        {
            return _matches(from.Date, to.Date);
        }

        event EventHandler ICommand.CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _apply();
        }
    }

    public sealed class StockReportTabViewModel : WarehouseReportTabViewModel
    {
        private ObservableCollection<IngredientRowViewModel> _items = new ObservableCollection<IngredientRowViewModel>();
        private StockReport _report;
        private bool _isEmpty = true;

        public StockReportTabViewModel()
            : base(
                WarehouseReportKind.Stock,
                "Остатки",
                "Остатки склада на дату, стоимость по закупочной цене и статус позиций.",
                false)
        {
        }

        public ObservableCollection<IngredientRowViewModel> Items
        {
            get { return _items; }
            private set { Set(ref _items, value); }
        }

        public StockReport Report
        {
            get { return _report; }
            private set { Set(ref _report, value); }
        }

        public bool IsEmpty
        {
            get { return _isEmpty; }
            private set { Set(ref _isEmpty, value); }
        }

        public string EmptyText
        {
            get
            {
                return "На складе пока нет позиций. Отчёт заполнится после добавления позиций в разделе «Склад».";
            }
        }

        public string GeneratedAtText
        {
            get
            {
                return _report == null
                    ? string.Empty
                    : "Сформирован " + _report.GeneratedAt.ToString("dd.MM.yyyy в HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
            }
        }

        internal override object BuildSnapshot()
        {
            return ReportService.GetStockReport();
        }

        internal override ReportTable BuildTable(IWarehouseReportExporter exporter)
        {
            return exporter.BuildStockReport((StockReport)BuildSnapshot());
        }

        internal override void Apply(object snapshot)
        {
            var report = snapshot as StockReport ?? throw new ArgumentException("Ожидался отчёт об остатках.", nameof(snapshot));

            Report = report;
            Items = new ObservableCollection<IngredientRowViewModel>(
                report.Ingredients.Select(ingredient => new IngredientRowViewModel(ingredient)));
            IsEmpty = report.Ingredients.Count == 0;
            HasSnapshot = true;
            OnPropertyChanged(nameof(GeneratedAtText));
        }

        internal override void Clear()
        {
            Report = null;
            Items = new ObservableCollection<IngredientRowViewModel>();
            IsEmpty = true;
            HasSnapshot = false;
            OnPropertyChanged(nameof(GeneratedAtText));
        }
    }

    public sealed class TurnoverReportTabViewModel : PeriodReportTabViewModel
    {
        private ObservableCollection<TurnoverRowViewModel> _items = new ObservableCollection<TurnoverRowViewModel>();
        private TurnoverReport _report;

        public TurnoverReportTabViewModel()
            : base(
                WarehouseReportKind.Turnover,
                "Обороты",
                "Приход и списание по каждой позиции за период, начальный и конечный остаток.")
        {
        }

        public ObservableCollection<TurnoverRowViewModel> Items
        {
            get { return _items; }
            private set { Set(ref _items, value); }
        }

        public TurnoverReport Report
        {
            get { return _report; }
            private set { Set(ref _report, value); }
        }

        public bool IsEmpty
        {
            get { return _items.Count == 0; }
        }

        public string EmptyText
        {
            get { return "За выбранный период движений по складу не было."; }
        }

        public string SummaryText
        {
            get
            {
                if (_report == null)
                {
                    return string.Empty;
                }

                return "Позиций с движением: " + _report.PositionsWithMovementCount +
                       " • операций: " + _report.OperationsCount;
            }
        }

        internal override object BuildSnapshot()
        {
            return ReportService.GetTurnoverReport(PeriodFrom, PeriodTo);
        }

        internal override ReportTable BuildTable(IWarehouseReportExporter exporter)
        {
            return exporter.BuildTurnoverReport((TurnoverReport)BuildSnapshot());
        }

        internal override void Apply(object snapshot)
        {
            var report = snapshot as TurnoverReport ?? throw new ArgumentException("Ожидался отчёт об оборотах.", nameof(snapshot));

            Report = report;
            Items = new ObservableCollection<TurnoverRowViewModel>(
                report.Items.Select(item => new TurnoverRowViewModel(item)));
            HasSnapshot = true;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(SummaryText));
        }

        internal override void Clear()
        {
            Report = null;
            Items = new ObservableCollection<TurnoverRowViewModel>();
            HasSnapshot = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    public sealed class DeficitReportTabViewModel : WarehouseReportTabViewModel
    {
        private ObservableCollection<DeficitRowViewModel> _items = new ObservableCollection<DeficitRowViewModel>();
        private DeficitReport _report;

        public DeficitReportTabViewModel()
            : base(
                WarehouseReportKind.Deficit,
                "Дефицит",
                "Позиции на минимальном остатке или ниже и рекомендуемое количество к закупке.",
                false)
        {
        }

        public ObservableCollection<DeficitRowViewModel> Items
        {
            get { return _items; }
            private set { Set(ref _items, value); }
        }

        public DeficitReport Report
        {
            get { return _report; }
            private set { Set(ref _report, value); }
        }

        public bool IsEmpty
        {
            get { return _items.Count == 0; }
        }

        public string EmptyText
        {
            get { return "Дефицита нет: все позиции с минимальным остатком выше минимума."; }
        }

        public decimal TotalPurchaseCost
        {
            get { return _items.Sum(item => item.PurchaseCost); }
        }

        internal override object BuildSnapshot()
        {
            return ReportService.GetDeficitReport();
        }

        internal override ReportTable BuildTable(IWarehouseReportExporter exporter)
        {
            return exporter.BuildDeficitReport((DeficitReport)BuildSnapshot());
        }

        internal override void Apply(object snapshot)
        {
            var report = snapshot as DeficitReport ?? throw new ArgumentException("Ожидался отчёт о дефиците.", nameof(snapshot));

            Report = report;
            Items = new ObservableCollection<DeficitRowViewModel>(
                report.Items.Select(item => new DeficitRowViewModel(item)));
            HasSnapshot = true;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(TotalPurchaseCost));
        }

        internal override void Clear()
        {
            Report = null;
            Items = new ObservableCollection<DeficitRowViewModel>();
            HasSnapshot = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(TotalPurchaseCost));
        }
    }

    public sealed class MovementsReportTabViewModel : PeriodReportTabViewModel
    {
        private ObservableCollection<MovementRowViewModel> _items = new ObservableCollection<MovementRowViewModel>();
        private MovementReport _report;

        public MovementsReportTabViewModel()
            : base(
                WarehouseReportKind.Movements,
                "Движение",
                "Журнал операций за период: приход, списание и начальные остатки.")
        {
        }

        public ObservableCollection<MovementRowViewModel> Items
        {
            get { return _items; }
            private set { Set(ref _items, value); }
        }

        public MovementReport Report
        {
            get { return _report; }
            private set { Set(ref _report, value); }
        }

        public bool IsEmpty
        {
            get { return _items.Count == 0; }
        }

        public string EmptyText
        {
            get { return "За выбранный период операций не было."; }
        }

        public string SummaryText
        {
            get
            {
                if (_report == null)
                {
                    return string.Empty;
                }

                return "Всего операций: " + _report.Movements.Count +
                       " • приход: " + _report.ReceiptCount +
                       " • списание: " + _report.WriteOffCount;
            }
        }

        internal override object BuildSnapshot()
        {
            return ReportService.GetMovementReport(PeriodFrom, PeriodTo);
        }

        internal override ReportTable BuildTable(IWarehouseReportExporter exporter)
        {
            return exporter.BuildMovementReport((MovementReport)BuildSnapshot());
        }

        internal override void Apply(object snapshot)
        {
            var report = snapshot as MovementReport ?? throw new ArgumentException("Ожидался отчёт о движении.", nameof(snapshot));

            Report = report;
            Items = new ObservableCollection<MovementRowViewModel>(
                report.Movements.Select(movement => new MovementRowViewModel(movement)));
            HasSnapshot = true;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(SummaryText));
        }

        internal override void Clear()
        {
            Report = null;
            Items = new ObservableCollection<MovementRowViewModel>();
            HasSnapshot = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(SummaryText));
        }
    }
}
