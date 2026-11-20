using System;
using System.Collections.Generic;
using System.Globalization;

namespace CenterCoffee.Application.Abstractions
{
    /// <summary>Выравнивание значения в ячейке отчёта.</summary>
    public enum ReportCellAlignment
    {
        Left = 1,
        Center = 2,
        Right = 3
    }

    /// <summary>Тип значения ячейки: от него зависят формат даты и числа в файле.</summary>
    public enum ReportCellKind
    {
        Text = 1,
        Number = 2,
        Money = 3,
        Date = 4,

        /// <summary>
        /// Изменение величины со знаком: приход положителен, списание отрицательно.
        /// В файле отображается со знаком, чтобы направление движения читалось сразу.
        /// </summary>
        Delta = 5
    }

    /// <summary>Смысловая окраска строки: например, дефицитные позиции выделяются тревожно.</summary>
    public enum ReportRowTone
    {
        Default = 1,
        Positive = 2,
        Warning = 3,
        Danger = 4,
        Muted = 5
    }

    /// <summary>
    /// Описание колонки отчёта: заголовок, предполагаемая ширина в символах и формат
    /// значений. Ширина задаётся здесь, а не в файле, чтобы вариант «широкие колонки
    /// для длинного текста» был виден в одном месте.
    /// </summary>
    public sealed class ReportColumn
    {
        public ReportColumn(
            string header,
            double width,
            ReportCellKind kind,
            ReportCellAlignment alignment)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));

            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Ширина колонки должна быть положительной.");
            }

            Width = width;
            Kind = kind;
            Alignment = alignment;
        }

        public string Header { get; private set; }

        public double Width { get; private set; }

        public ReportCellKind Kind { get; private set; }

        /// <summary>Выравнивание заголовка и готовых текстовых значений.</summary>
        public ReportCellAlignment Alignment { get; private set; }
    }

    /// <summary>
    /// Строка отчёта. Числа и даты хранятся значениями, а не готовым текстом:
    /// поэтому в Excel они остаются числами и сортируются как числа.
    /// </summary>
    public sealed class ReportRow
    {
        private readonly Dictionary<int, ReportCell> _cells;

        public ReportRow(ReportRowTone tone)
        {
            Tone = tone;
            _cells = new Dictionary<int, ReportCell>();
        }

        public ReportRowTone Tone { get; private set; }

        /// <summary>Текстовая ячейка: выравнивается по описанию колонки.</summary>
        public ReportRow Text(int index, string text)
        {
            return Set(index, new ReportCell { Kind = ReportCellKind.Text, Text = text ?? string.Empty });
        }

        /// <summary>Числовая ячейка без денежного формата.</summary>
        public ReportRow Number(int index, decimal value)
        {
            return Set(index, new ReportCell { Kind = ReportCellKind.Number, Number = value });
        }

        /// <summary>Денежная ячейка: в файле отображается с рублём.</summary>
        public ReportRow Money(int index, decimal value)
        {
            return Set(index, new ReportCell { Kind = ReportCellKind.Money, Number = value });
        }

        /// <summary>
        /// Ячейка изменения со знаком. Значение хранится со знаком: приход как есть,
        /// списание отрицательным числом.
        /// </summary>
        public ReportRow Delta(int index, decimal value)
        {
            return Set(index, new ReportCell { Kind = ReportCellKind.Delta, Number = value });
        }

        /// <summary>Ячейка даты и времени, сохраняемая как настоящая дата Excel.</summary>
        public ReportRow Moment(int index, DateTime value)
        {
            return Set(index, new ReportCell { Kind = ReportCellKind.Date, Moment = value });
        }

        /// <summary>Текст конкретной ячейки или null, если значения нет.</summary>
        public string GetText(int index)
        {
            ReportCell cell;
            if (!_cells.TryGetValue(index, out cell))
            {
                return null;
            }

            return cell.Kind == ReportCellKind.Text ? cell.Text : null;
        }

        private ReportRow Set(int index, ReportCell cell)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            // Повторная запись той же ячейки — ошибка формирования отчёта, а не повод
            // молча перезаписать значение.
            if (_cells.ContainsKey(index))
            {
                throw new InvalidOperationException(
                    "В строке отчёта ячейка " + index.ToString(CultureInfo.InvariantCulture) + " уже заполнена.");
            }

            _cells.Add(index, cell);
            return this;
        }

        /// <summary>
        /// Значение ячейки по индексу колонки. Нужен формирователю файла: он получает
        /// типизированное значение, а не разбирает готовый текст обратно.
        /// </summary>
        public bool TryGetCell(int index, out ReportCell cell)
        {
            return _cells.TryGetValue(index, out cell);
        }
    }

    /// <summary>Значение ячейки отчёта. Используется формирователем файла, а не UI.</summary>
    public sealed class ReportCell
    {
        public ReportCellKind Kind { get; set; }

        public string Text { get; set; }

        public decimal Number { get; set; }

        public DateTime Moment { get; set; }
    }

    /// <summary>
    /// Готовый табличный отчёт: заголовок, сводка в подписях, колонки и строки.
    /// Содержит только данные и описание оформления, без знания о CSV, Excel и WPF.
    /// </summary>
    public sealed class ReportTable
    {
        private readonly List<ReportRow> _rows;

        public ReportTable(string title, IEnumerable<ReportColumn> columns)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Не задан заголовок отчёта.", nameof(title));
            }

            if (columns == null)
            {
                throw new ArgumentNullException(nameof(columns));
            }

            Title = title;
            Columns = new List<ReportColumn>(columns);

            if (Columns.Count == 0)
            {
                throw new ArgumentException("У отчёта должна быть хотя бы одна колонка.", nameof(columns));
            }

            Summary = new List<KeyValuePair<string, string>>();
            _rows = new List<ReportRow>();
        }

        public string Title { get; private set; }

        /// <summary>Подписи над таблицей: период, дата формирования, количество позиций.</summary>
        public IList<KeyValuePair<string, string>> Summary { get; private set; }

        public IReadOnlyList<ReportColumn> Columns { get; private set; }

        public IReadOnlyList<ReportRow> Rows
        {
            get { return _rows; }
        }

        /// <summary>Пояснение под таблицей; null, если пояснения нет.</summary>
        public string Note { get; set; }

        /// <summary>Добавляет подпись сводки. Значение — уже готовый текст для человека.</summary>
        public ReportTable AddSummary(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Не задана подпись сводки.", nameof(label));
            }

            Summary.Add(new KeyValuePair<string, string>(label, value ?? string.Empty));
            return this;
        }

        /// <summary>Добавляет подпись сводки числом: в файле оно останется числом.</summary>
        public ReportTable AddSummary(string label, int value)
        {
            return AddSummary(label, value.ToString(CultureInfo.GetCultureInfo("ru-RU")));
        }

        public ReportTable AddRow(ReportRow row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            _rows.Add(row);
            return this;
        }
    }
}
