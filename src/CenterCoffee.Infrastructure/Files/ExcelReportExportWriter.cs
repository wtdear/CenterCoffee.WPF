using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using CenterCoffee.Application.Abstractions;

namespace CenterCoffee.Infrastructure.Files
{
    /// <summary>
    /// Запись отчётов в формат Excel Open XML (.xlsx) средствами .NET Framework
    /// без внешних библиотек. Книга собирается как ZIP-пакет с правильным манифестом
    /// [Content_Types].xml и отношениями, поэтому Excel открывает файл без запросов.
    /// Лист содержит сводку, заголовок, ширины колонок, закреплённую область,
    /// автофильтр и окраску строк, чтобы отчёт читался сразу.
    /// Запись идёт через временный файл и подмену, как в прежнем текстовом выгрузчике.
    /// </summary>
    public sealed class ExcelReportExportWriter : IReportExportWriter
    {
        private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string OfficeDocumentRelationship =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
        private const string WorksheetRelationship =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
        private const string StylesRelationship =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";

        private const string WorkbookContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
        private const string WorksheetContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
        private const string StylesContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml";

        public string Write(ReportTable table, string filePath)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Не задан путь для сохранения отчёта.", nameof(filePath));
            }

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = fullPath + ".tmp";

            try
            {
                WritePackage(temporaryPath, table);

                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, null);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                CleanupTemporary(temporaryPath);
            }

            return fullPath;
        }

        private static void WritePackage(string temporaryPath, ReportTable table)
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                var styleBook = new StyleBook();

                WriteContentTypes(archive);
                WriteRootRelationships(archive);
                WriteWorkbook(archive);
                WriteWorkbookRelationships(archive);

                // Лист сначала заполняет реестр стилей, и только потом пишется
                // styles.xml: иначе ячейки ссылались бы на несуществующие стили.
                WriteWorksheet(archive, table, styleBook);
                WriteStyles(archive, styleBook);
            }
        }

        private static void WriteContentTypes(ZipArchive archive)
        {
            using (var writer = CreateWriter(archive, "[Content_Types].xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                WriteDefaultContentType(writer, "rels",
                    "application/vnd.openxmlformats-package.relationships+xml");
                WriteDefaultContentType(writer, "xml", "application/xml");
                WriteOverrideContentType(writer, "/xl/workbook.xml", WorkbookContentType);
                WriteOverrideContentType(writer, "/xl/worksheets/sheet1.xml", WorksheetContentType);
                WriteOverrideContentType(writer, "/xl/styles.xml", StylesContentType);
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteDefaultContentType(XmlWriter writer, string extension, string contentType)
        {
            writer.WriteStartElement("Default",
                "http://schemas.openxmlformats.org/package/2006/content-types");
            writer.WriteAttributeString("Extension", extension);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteOverrideContentType(XmlWriter writer, string partName, string contentType)
        {
            writer.WriteStartElement("Override",
                "http://schemas.openxmlformats.org/package/2006/content-types");
            writer.WriteAttributeString("PartName", partName);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteRootRelationships(ZipArchive archive)
        {
            using (var writer = CreateWriter(archive, "_rels/.rels"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Relationships",
                    "http://schemas.openxmlformats.org/package/2006/relationships");
                WriteRelationship(writer, "rId1", OfficeDocumentRelationship, "xl/workbook.xml");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteWorkbookRelationships(ZipArchive archive)
        {
            using (var writer = CreateWriter(archive, "xl/_rels/workbook.xml.rels"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Relationships",
                    "http://schemas.openxmlformats.org/package/2006/relationships");
                WriteRelationship(writer, "rId1", WorksheetRelationship, "worksheets/sheet1.xml");
                WriteRelationship(writer, "rId2", StylesRelationship, "styles.xml");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
        {
            writer.WriteStartElement("Relationship",
                "http://schemas.openxmlformats.org/package/2006/relationships");
            writer.WriteAttributeString("Id", id);
            writer.WriteAttributeString("Type", type);
            writer.WriteAttributeString("Target", target);
            writer.WriteEndElement();
        }

        private static void WriteWorkbook(ZipArchive archive)
        {
            using (var writer = CreateWriter(archive, "xl/workbook.xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("workbook", MainNamespace);
                writer.WriteAttributeString("xmlns", "r", null,
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                writer.WriteStartElement("sheets", MainNamespace);
                writer.WriteStartElement("sheet", MainNamespace);
                writer.WriteAttributeString("name", "Отчёт");
                writer.WriteAttributeString("sheetId", "1");
                writer.WriteAttributeString("r", "id",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "rId1");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteStyles(ZipArchive archive, StyleBook styleBook)
        {
            using (var writer = CreateWriter(archive, "xl/styles.xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("styleSheet", MainNamespace);

                writer.WriteStartElement("numFmts", MainNamespace);
                writer.WriteAttributeString("count",
                    styleBook.NumberFormats.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var numberFormat in styleBook.NumberFormats)
                {
                    writer.WriteStartElement("numFmt", MainNamespace);
                    writer.WriteAttributeString("numFmtId",
                        numberFormat.Key.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("formatCode", numberFormat.Value);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteStartElement("fonts", MainNamespace);
                writer.WriteAttributeString("count", styleBook.Fonts.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var font in styleBook.Fonts)
                {
                    WriteFont(writer, font);
                }
                writer.WriteEndElement();

                // Позиции заливок: 0 — нет, 1 — gray125 (обязательные), дальше рабочие.
                writer.WriteStartElement("fills", MainNamespace);
                writer.WriteAttributeString("count",
                    (styleBook.Fills.Count + 2).ToString(CultureInfo.InvariantCulture));

                writer.WriteStartElement("fill", MainNamespace);
                writer.WriteStartElement("patternFill", MainNamespace);
                writer.WriteAttributeString("patternType", "none");
                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteStartElement("fill", MainNamespace);
                writer.WriteStartElement("patternFill", MainNamespace);
                writer.WriteAttributeString("patternType", "gray125");
                writer.WriteEndElement();
                writer.WriteEndElement();

                foreach (var fill in styleBook.Fills)
                {
                    writer.WriteStartElement("fill", MainNamespace);
                    writer.WriteStartElement("patternFill", MainNamespace);
                    writer.WriteAttributeString("patternType", "solid");
                    writer.WriteStartElement("fgColor", MainNamespace);
                    writer.WriteAttributeString("rgb", fill.ColorHex);
                    writer.WriteEndElement();
                    writer.WriteStartElement("bgColor", MainNamespace);
                    writer.WriteAttributeString("indexed", "64");
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteStartElement("borders", MainNamespace);
                writer.WriteAttributeString("count", "2");
                WriteBorder(writer, false);
                WriteBorder(writer, true);
                writer.WriteEndElement();

                writer.WriteStartElement("cellStyleXfs", MainNamespace);
                writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("xf", MainNamespace);
                writer.WriteAttributeString("numFmtId", "0");
                writer.WriteAttributeString("fontId", "0");
                writer.WriteAttributeString("fillId", "0");
                writer.WriteAttributeString("borderId", "0");
                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteStartElement("cellXfs", MainNamespace);
                writer.WriteAttributeString("count",
                    styleBook.CellStyles.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var cellStyle in styleBook.CellStyles)
                {
                    WriteCellFormat(writer, cellStyle);
                }
                writer.WriteEndElement();

                writer.WriteStartElement("cellStyles", MainNamespace);
                writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("cellStyle", MainNamespace);
                writer.WriteAttributeString("name", "Normal");
                writer.WriteAttributeString("xfId", "0");
                writer.WriteAttributeString("builtinId", "0");
                writer.WriteEndElement();
                writer.WriteEndElement();

                writer.WriteStartElement("dxfs", MainNamespace);
                writer.WriteAttributeString("count", "0");
                writer.WriteEndElement();

                writer.WriteStartElement("tableStyles", MainNamespace);
                writer.WriteAttributeString("count", "0");
                writer.WriteAttributeString("defaultTableStyle", "TableStyleMedium2");
                writer.WriteAttributeString("defaultPivotStyle", "PivotStyleLight16");
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteCellFormat(XmlWriter writer, StyleBook.CellStyleEntry cellStyle)
        {
            writer.WriteStartElement("xf", MainNamespace);
            writer.WriteAttributeString("numFmtId", cellStyle.NumberFormatId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fontId", cellStyle.FontId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fillId", cellStyle.FillId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("borderId", cellStyle.BorderId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("applyNumberFormat", cellStyle.NumberFormatId != 0 ? "1" : "0");
            writer.WriteAttributeString("applyFont", cellStyle.FontId != 0 ? "1" : "0");
            writer.WriteAttributeString("applyFill", cellStyle.FillId != 0 ? "1" : "0");
            writer.WriteAttributeString("applyBorder", cellStyle.BorderId != 0 ? "1" : "0");
            if (cellStyle.Horizontal.HasValue)
            {
                writer.WriteAttributeString("applyAlignment", "1");
                writer.WriteStartElement("alignment", MainNamespace);
                writer.WriteAttributeString("horizontal", GetHorizontalName(cellStyle.Horizontal.Value));
                writer.WriteAttributeString("vertical", "center");
                writer.WriteAttributeString("wrapText", cellStyle.Wrap ? "1" : "0");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteFont(XmlWriter writer, StyleBook.StyleFont font)
        {
            writer.WriteStartElement("font", MainNamespace);
            if (font.Size.HasValue)
            {
                writer.WriteStartElement("sz", MainNamespace);
                writer.WriteAttributeString("val", font.Size.Value.ToString("0.##", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }
            if (font.Bold)
            {
                writer.WriteStartElement("b", MainNamespace);
                writer.WriteAttributeString("val", "1");
                writer.WriteEndElement();
            }
            if (font.Italic)
            {
                writer.WriteStartElement("i", MainNamespace);
                writer.WriteAttributeString("val", "1");
                writer.WriteEndElement();
            }
            if (font.ColorHex != null)
            {
                writer.WriteStartElement("color", MainNamespace);
                writer.WriteAttributeString("rgb", font.ColorHex);
                writer.WriteEndElement();
            }
            writer.WriteStartElement("name", MainNamespace);
            writer.WriteAttributeString("val", font.Name);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteBorder(XmlWriter writer, bool thin)
        {
            writer.WriteStartElement("border", MainNamespace);
            writer.WriteAttributeString("diagonalUp", "0");
            writer.WriteAttributeString("diagonalDown", "0");
            foreach (var side in new[] { "left", "right", "top", "bottom" })
            {
                writer.WriteStartElement(side, MainNamespace);
                if (thin)
                {
                    writer.WriteAttributeString("style", "thin");
                    writer.WriteStartElement("color", MainNamespace);
                    writer.WriteAttributeString("rgb", "FFD5D9E0");
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static string GetHorizontalName(ReportCellAlignment alignment)
        {
            switch (alignment)
            {
                case ReportCellAlignment.Center:
                    return "center";
                case ReportCellAlignment.Right:
                    return "right";
                default:
                    return "left";
            }
        }

        private static void WriteWorksheet(ZipArchive archive, ReportTable table, StyleBook styleBook)
        {
            using (var writer = CreateWriter(archive, "xl/worksheets/sheet1.xml"))
            {
                WriteSheet(writer, table, styleBook);
            }
        }

        private static void WriteSheet(XmlWriter writer, ReportTable table, StyleBook styleBook)
        {
            var columnCount = table.Columns.Count;
            var lastColumnLetter = ColumnLetter(columnCount);
            var summaryCount = table.Summary.Count;

            // Строки: 1 — заголовок отчёта, 2..1+summary — сводка, затем пустая
            // строка-отступ, строка колонок, данные и примечание.
            var headerRow = summaryCount + 3;
            if (summaryCount == 0)
            {
                headerRow = 2;
            }
            var firstDataRow = headerRow + 1;
            var lastDataRow = firstDataRow + table.Rows.Count - 1;
            var noteRow = table.Note != null ? Math.Max(lastDataRow, headerRow) + 1 : 0;
            var lastRow = Math.Max(noteRow, Math.Max(lastDataRow, headerRow));

            var titleStyle = styleBook.CellStyle(StyleBook.StyleKind.Title);
            var summaryLabelStyle = styleBook.CellStyle(StyleBook.StyleKind.SummaryLabel);
            var summaryValueStyle = styleBook.CellStyle(StyleBook.StyleKind.SummaryValue);
            var headerStyle = styleBook.HeaderStyle();

            writer.WriteStartDocument();
            writer.WriteStartElement("worksheet", MainNamespace);

            WriteDimension(writer, lastColumnLetter, lastRow);

            // Закрепление области сверху: заголовок отчёта, сводка и строка колонок
            // остаются видимыми при прокрутке длинного списка.
            writer.WriteStartElement("sheetViews", MainNamespace);
            writer.WriteStartElement("sheetView", MainNamespace);
            writer.WriteAttributeString("workbookViewId", "0");
            writer.WriteStartElement("pane", MainNamespace);
            writer.WriteAttributeString("ySplit", headerRow.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("topLeftCell", "A" + (headerRow + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("activePane", "bottomLeft");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("sheetFormatPr", MainNamespace);
            writer.WriteAttributeString("defaultRowHeight", "16.5");
            writer.WriteEndElement();

            WriteColumnWidths(writer, table);

            writer.WriteStartElement("sheetData", MainNamespace);

            writer.WriteStartElement("row", MainNamespace);
            writer.WriteAttributeString("r", "1");
            writer.WriteAttributeString("ht", "24");
            writer.WriteAttributeString("customHeight", "1");
            WriteTextCell(writer, 1, 1, titleStyle, table.Title);
            writer.WriteEndElement();

            for (var index = 0; index < summaryCount; index++)
            {
                var rowNumber = index + 2;
                writer.WriteStartElement("row", MainNamespace);
                writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
                WriteTextCell(writer, rowNumber, 1, summaryLabelStyle, table.Summary[index].Key);
                WriteTextCell(writer, rowNumber, 2, summaryValueStyle, table.Summary[index].Value);
                writer.WriteEndElement();
            }

            if (summaryCount > 0)
            {
                writer.WriteStartElement("row", MainNamespace);
                writer.WriteAttributeString("r", (summaryCount + 2).ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }

            writer.WriteStartElement("row", MainNamespace);
            writer.WriteAttributeString("r", headerRow.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("ht", "28");
            writer.WriteAttributeString("customHeight", "1");
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                WriteTextCell(writer, headerRow, columnIndex + 1, headerStyle, table.Columns[columnIndex].Header);
            }
            writer.WriteEndElement();

            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                var rowNumber = firstDataRow + rowIndex;
                var banded = rowIndex % 2 == 1;

                writer.WriteStartElement("row", MainNamespace);
                writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    var column = table.Columns[columnIndex];
                    var style = styleBook.DataCellStyle(
                        column.Kind,
                        column.Alignment,
                        row.Tone,
                        banded,
                        HasFraction(row, columnIndex));
                    WriteDataCell(writer, row, rowNumber, columnIndex, style);
                }
                writer.WriteEndElement();
            }

            if (noteRow > 0)
            {
                var noteStyle = styleBook.CellStyle(StyleBook.StyleKind.Note);
                writer.WriteStartElement("row", MainNamespace);
                writer.WriteAttributeString("r", noteRow.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("ht", "18");
                writer.WriteAttributeString("customHeight", "1");
                WriteTextCell(writer, noteRow, 1, noteStyle, table.Note);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();

            if (table.Rows.Count > 0)
            {
                writer.WriteStartElement("autoFilter", MainNamespace);
                writer.WriteAttributeString("ref",
                    "A" + headerRow.ToString(CultureInfo.InvariantCulture) + ":" +
                    lastColumnLetter + lastDataRow.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }

            WriteMergeRanges(writer, table, columnCount, summaryCount, noteRow);

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private static bool HasFraction(ReportRow row, int columnIndex)
        {
            ReportCell cell;
            if (!row.TryGetCell(columnIndex, out cell))
            {
                return false;
            }

            // Дробность определяется по значению, чтобы целые количества
            // не показывали лишний знак после разделителя.
            return cell.Number != decimal.Truncate(cell.Number);
        }

        private static void WriteDataCell(
            XmlWriter writer,
            ReportRow row,
            int rowNumber,
            int columnIndex,
            StyleBook.CellStyleReference style)
        {
            ReportCell cell;
            var hasValue = row.TryGetCell(columnIndex, out cell);
            var reference = ColumnLetter(columnIndex + 1) + rowNumber.ToString(CultureInfo.InvariantCulture);

            writer.WriteStartElement("c", MainNamespace);
            writer.WriteAttributeString("r", reference);
            writer.WriteAttributeString("s", style.Index.ToString(CultureInfo.InvariantCulture));

            if (!hasValue)
            {
                writer.WriteEndElement();
                return;
            }

            switch (cell.Kind)
            {
                case ReportCellKind.Date:
                    writer.WriteElementString("v", MainNamespace,
                        ToExcelSerial(cell.Moment).ToString("0.######", CultureInfo.InvariantCulture));
                    break;
                case ReportCellKind.Number:
                case ReportCellKind.Money:
                case ReportCellKind.Delta:
                    writer.WriteElementString("v", MainNamespace, cell.Number.ToString(CultureInfo.InvariantCulture));
                    break;
                default:
                    writer.WriteAttributeString("t", "inlineStr");
                    writer.WriteStartElement("is", MainNamespace);
                    WriteTextValue(writer, cell.Text);
                    writer.WriteEndElement();
                    break;
            }

            writer.WriteEndElement();
        }

        private static void WriteTextCell(
            XmlWriter writer,
            int rowNumber,
            int columnNumber,
            StyleBook.CellStyleReference style,
            string text)
        {
            var reference = ColumnLetter(columnNumber) + rowNumber.ToString(CultureInfo.InvariantCulture);

            writer.WriteStartElement("c", MainNamespace);
            writer.WriteAttributeString("r", reference);
            writer.WriteAttributeString("s", style.Index.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", MainNamespace);
            WriteTextValue(writer, text);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteTextValue(XmlWriter writer, string text)
        {
            writer.WriteStartElement("t", MainNamespace);
            if (NeedsSpacePreserve(text))
            {
                writer.WriteAttributeString("xml", "space", null, "preserve");
            }
            writer.WriteString(SanitizeForXml(text));
            writer.WriteEndElement();
        }

        private static void WriteDimension(XmlWriter writer, string lastColumnLetter, int lastRow)
        {
            writer.WriteStartElement("dimension", MainNamespace);
            writer.WriteAttributeString("ref",
                "A1:" + lastColumnLetter + lastRow.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static void WriteColumnWidths(XmlWriter writer, ReportTable table)
        {
            writer.WriteStartElement("cols", MainNamespace);
            for (var index = 0; index < table.Columns.Count; index++)
            {
                writer.WriteStartElement("col", MainNamespace);
                writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("width",
                    table.Columns[index].Width.ToString("0.##", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("customWidth", "1");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteMergeRanges(
            XmlWriter writer,
            ReportTable table,
            int columnCount,
            int summaryCount,
            int noteRow)
        {
            var ranges = new List<string> { "A1:" + ColumnLetter(columnCount) + "1" };

            for (var index = 0; index < summaryCount; index++)
            {
                var rowNumber = index + 2;
                ranges.Add("B" + rowNumber.ToString(CultureInfo.InvariantCulture) + ":C" +
                           rowNumber.ToString(CultureInfo.InvariantCulture));
            }

            if (noteRow > 0)
            {
                ranges.Add("A" + noteRow.ToString(CultureInfo.InvariantCulture) + ":" +
                           ColumnLetter(columnCount) + noteRow.ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteStartElement("mergeCells", MainNamespace);
            writer.WriteAttributeString("count", ranges.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var range in ranges)
            {
                writer.WriteStartElement("mergeCell", MainNamespace);
                writer.WriteAttributeString("ref", range);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static XmlWriter CreateWriter(ZipArchive archive, string entryName)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                CloseOutput = true
            };
            return XmlWriter.Create(entry.Open(), settings);
        }

        private static string ColumnLetter(int columnNumber)
        {
            var value = string.Empty;
            var current = columnNumber;
            while (current > 0)
            {
                var remainder = (current - 1) % 26;
                value = (char)('A' + remainder) + value;
                current = (current - 1) / 26;
            }
            return value;
        }

        /// <summary>Сериализация даты Excel: количество дней от 30.12.1899.</summary>
        private static double ToExcelSerial(DateTime moment)
        {
            var excelEpoch = new DateTime(1899, 12, 30);
            return (moment - excelEpoch).TotalDays;
        }

        private static bool NeedsSpacePreserve(string text)
        {
            return text != null &&
                   (text.Length == 0 ||
                    text[0] == ' ' ||
                    text[text.Length - 1] == ' ' ||
                    text.IndexOf('\t') >= 0 ||
                    text.IndexOf('\n') >= 0);
        }

        /// <summary>
        /// Убирает управляющие символы, недопустимые в XML 1.0: без этого Excel
        /// сообщит о повреждении файла, если в данных окажется служебный символ.
        /// </summary>
        private static string SanitizeForXml(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var hasInvalid = false;
            for (var index = 0; index < text.Length; index++)
            {
                if (!IsValidXmlChar(text[index]))
                {
                    hasInvalid = true;
                    break;
                }
            }

            if (!hasInvalid)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                if (IsValidXmlChar(character))
                {
                    builder.Append(character);
                }
            }
            return builder.ToString();
        }

        private static bool IsValidXmlChar(char character)
        {
            return character == 0x9 ||
                   character == 0xA ||
                   character == 0xD ||
                   (character >= 0x20 && character <= 0xD7FF) ||
                   (character >= 0xE000 && character <= 0xFFFD);
        }

        private static void CleanupTemporary(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Реестр стилей одного файла. Ячеечные стили добавляются лениво во время
        /// записи листа, поэтому в styles.xml попадают только используемые наборы.
        /// Позиция стиля в <c>CellStyles</c> совпадает со значением s в листе.
        /// </summary>
        internal sealed class StyleBook
        {
            private const string MutedText = "FF595959";
            private const string DangerText = "FF9C0006";
            private const string WarningText = "FF9C6500";
            private const string PositiveText = "FF006100";
            private const string TitleText = "FF1F3864";
            private const string HeaderFill = "FF1F3864";
            private const string BandedFill = "FFF2F6FA";
            private const string WarningFill = "FFFFEB9C";
            private const string DangerFill = "FFFFC7CE";
            private const string PositiveFill = "FFC6EFCE";
            private const string MutedFill = "FFF5F5F5";

            private const string Ruble = "\u20BD";

            private readonly Dictionary<string, CellStyleReference> _cellStyleIndexes =
                new Dictionary<string, CellStyleReference>();
            private readonly List<CellStyleEntry> _cellStyles = new List<CellStyleEntry>();

            public StyleBook()
            {
                // Позиция 0 — нейтральный стиль по умолчанию; добавленные стили
                // нумеруются с единицы, чтобы совпасть с индексами s в листе.
                _cellStyles.Add(new CellStyleEntry());

                Fonts.Add(FontRegular());
                Fonts.Add(FontTitle());
                Fonts.Add(FontHeader());
                Fonts.Add(FontBold(MutedText));
                Fonts.Add(FontItalic(MutedText));
                Fonts.Add(FontBold(DangerText));
                Fonts.Add(FontBold(WarningText));
                Fonts.Add(FontBold(PositiveText));

                // Позиции 0 и 1 из fills заняты пустой и gray125; рабочие идут со 2-й.
                Fills.Add(new FillDefinition(HeaderFill));
                Fills.Add(new FillDefinition(BandedFill));
                Fills.Add(new FillDefinition(WarningFill));
                Fills.Add(new FillDefinition(DangerFill));
                Fills.Add(new FillDefinition(PositiveFill));
                Fills.Add(new FillDefinition(MutedFill));
            }

            public enum StyleKind
            {
                Title = 1,
                SummaryLabel,
                SummaryValue,
                Note
            }

            public sealed class StyleFont
            {
                public double? Size;
                public bool Bold;
                public bool Italic;
                public string ColorHex;
                public string Name = "Calibri";
            }

            public sealed class FillDefinition
            {
                public FillDefinition(string colorHex)
                {
                    ColorHex = colorHex;
                }

                public string ColorHex { get; private set; }
            }

            public sealed class CellStyleEntry
            {
                public int NumberFormatId;
                public int FontId;
                public int FillId;
                public int BorderId;
                public ReportCellAlignment? Horizontal;
                public bool Wrap;
            }

            public sealed class CellStyleReference
            {
                public int Index;
            }

            public List<KeyValuePair<int, string>> NumberFormats { get; } = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(164, "#,##0.0##"),
                new KeyValuePair<int, string>(165, "#,##0.00 \"" + Ruble + "\""),
                new KeyValuePair<int, string>(166, "dd.MM.yyyy HH:mm"),
                new KeyValuePair<int, string>(167, "+#,##0.###;-#,##0.###;0"),
                new KeyValuePair<int, string>(168, "+#,##0.0##;-#,##0.0##;0"),
                new KeyValuePair<int, string>(169, "+#,##0;-#,##0;0"),
                new KeyValuePair<int, string>(170, "#,##0")
            };

            public List<StyleFont> Fonts { get; } = new List<StyleFont>();

            public List<FillDefinition> Fills { get; } = new List<FillDefinition>();

            public List<CellStyleEntry> CellStyles
            {
                get { return _cellStyles; }
            }

            public CellStyleReference CellStyle(StyleKind kind)
            {
                return GetOrAdd("s" + (int)kind, CreateCellStyle(kind));
            }

            public CellStyleReference HeaderStyle()
            {
                return GetOrAdd("header", new CellStyleEntry
                {
                    FontId = 2,
                    FillId = 2,
                    BorderId = 1,
                    Horizontal = ReportCellAlignment.Center,
                    Wrap = true
                });
            }

            public CellStyleReference DataCellStyle(
                ReportCellKind kind,
                ReportCellAlignment alignment,
                ReportRowTone tone,
                bool banded,
                bool fractional)
            {
                var key = ((int)kind).ToString(CultureInfo.InvariantCulture) + "." +
                          ((int)alignment).ToString(CultureInfo.InvariantCulture) + "." +
                          ((int)tone).ToString(CultureInfo.InvariantCulture) + "." +
                          (banded ? "b" : "n") + "." +
                          (fractional ? "f" : "i");
                return GetOrAdd("d" + key, CreateDataStyle(kind, alignment, tone, banded, fractional));
            }

            private CellStyleReference GetOrAdd(string key, CellStyleEntry entry)
            {
                CellStyleReference existing;
                if (_cellStyleIndexes.TryGetValue(key, out existing))
                {
                    return existing;
                }

                _cellStyles.Add(entry);
                var reference = new CellStyleReference { Index = _cellStyles.Count - 1 };
                _cellStyleIndexes.Add(key, reference);
                return reference;
            }

            private static CellStyleEntry CreateCellStyle(StyleKind kind)
            {
                switch (kind)
                {
                    case StyleKind.Title:
                        return new CellStyleEntry
                        {
                            FontId = 1,
                            Horizontal = ReportCellAlignment.Center
                        };
                    case StyleKind.SummaryLabel:
                        return new CellStyleEntry
                        {
                            FontId = 3,
                            Horizontal = ReportCellAlignment.Right
                        };
                    case StyleKind.SummaryValue:
                        return new CellStyleEntry
                        {
                            FontId = 0,
                            Horizontal = ReportCellAlignment.Left
                        };
                    case StyleKind.Note:
                        return new CellStyleEntry
                        {
                            FontId = 4,
                            Horizontal = ReportCellAlignment.Left,
                            Wrap = true
                        };
                    default:
                        return new CellStyleEntry();
                }
            }

            private static CellStyleEntry CreateDataStyle(
                ReportCellKind kind,
                ReportCellAlignment alignment,
                ReportRowTone tone,
                bool banded,
                bool fractional)
            {
                // Окраска строки: смысловые тона перекрывают чередование, поэтому
                // дефицитные и приходные строки видны даже на пёстром списке.
                var fillId = tone == ReportRowTone.Danger
                    ? 5
                    : tone == ReportRowTone.Warning
                        ? 4
                        : tone == ReportRowTone.Positive
                            ? 6
                            : tone == ReportRowTone.Muted
                                ? 7
                                : banded
                                    ? 3
                                    : 0;

                return new CellStyleEntry
                {
                    NumberFormatId = GetNumberFormatId(kind, fractional),
                    FontId = GetToneFontId(tone),
                    FillId = fillId,
                    BorderId = 1,
                    Horizontal = alignment
                };
            }

            private static int GetNumberFormatId(ReportCellKind kind, bool fractional)
            {
                switch (kind)
                {
                    case ReportCellKind.Money:
                        return 165;
                    case ReportCellKind.Date:
                        return 166;
                    // Целые количества со шкалой тысяч, дробные — до трёх знаков:
                    // формат «#,##0.###» показывал бы «4 200,» с висячей запятой.
                    case ReportCellKind.Delta:
                        return fractional ? 168 : 169;
                    case ReportCellKind.Number:
                        return fractional ? 164 : 170;
                    default:
                        return 0;
                }
            }

            private static int GetToneFontId(ReportRowTone tone)
            {
                switch (tone)
                {
                    case ReportRowTone.Danger:
                        return 5;
                    case ReportRowTone.Warning:
                        return 6;
                    case ReportRowTone.Positive:
                        return 7;
                    default:
                        return 0;
                }
            }

            private static StyleFont FontRegular()
            {
                return new StyleFont();
            }

            private static StyleFont FontTitle()
            {
                return new StyleFont
                {
                    Size = 14,
                    Bold = true,
                    ColorHex = TitleText
                };
            }

            private static StyleFont FontHeader()
            {
                return new StyleFont
                {
                    Bold = true,
                    ColorHex = "FFFFFFFF"
                };
            }

            private static StyleFont FontBold(string colorHex)
            {
                return new StyleFont
                {
                    Bold = true,
                    ColorHex = colorHex
                };
            }

            private static StyleFont FontItalic(string colorHex)
            {
                return new StyleFont
                {
                    Italic = true,
                    ColorHex = colorHex
                };
            }
        }
    }
}