using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AssetAppraisalChecker.Document.Excel
{
    /// <summary>Addressed cells of the selected worksheet; shared strings are workbook-level storage only.</summary>
    internal sealed class ExcelWorksheetCells
    {
        private static readonly Regex AddressPattern = new Regex(@"^(?<column>[A-Z]{1,3})(?<row>[1-9][0-9]{0,6})$",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private readonly Dictionary<string, Cell> _cells = new Dictionary<string, Cell>(StringComparer.Ordinal);
        private readonly List<CellRange> _merges = new List<CellRange>();
        private readonly SharedStringItem[] _sharedStrings;

        public ExcelWorksheetCells(WorkbookPart workbook, WorksheetPart worksheet)
        {
            _sharedStrings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().ToArray()
                ?? new SharedStringItem[0];
            SheetData data = worksheet.Worksheet.GetFirstChild<SheetData>();
            if (data == null) throw Invalid("指定工作表缺少单元格数据。");
            Rows = data.Elements<Row>().OrderBy(row => row.RowIndex?.Value ?? 0).ToArray();
            var indexes = new HashSet<uint>();
            foreach (Row row in Rows)
            {
                if (row.RowIndex == null || !indexes.Add(row.RowIndex.Value))
                    throw Invalid("工作表存在缺失或重复的行地址。");
                foreach (Cell cell in row.Elements<Cell>())
                {
                    CellPosition position = ParseAddress(cell.CellReference?.Value);
                    if (position.Row != row.RowIndex.Value || _cells.ContainsKey(cell.CellReference.Value))
                        throw Invalid("工作表存在不一致或重复的单元格地址。");
                    _cells.Add(cell.CellReference.Value, cell);
                }
            }
            foreach (MergeCell merge in worksheet.Worksheet.GetFirstChild<MergeCells>()?.Elements<MergeCell>()
                ?? Enumerable.Empty<MergeCell>())
            {
                string[] ends = (merge.Reference?.Value ?? string.Empty).Split(':');
                if (ends.Length != 2) throw Invalid("工作表存在无效的合并区域。");
                var range = new CellRange(ParseAddress(ends[0]), ParseAddress(ends[1]));
                if (range.Start.Row > range.End.Row || range.Start.Column > range.End.Column)
                    throw Invalid("工作表存在无效的合并区域。");
                _merges.Add(range);
            }
        }

        public IReadOnlyList<Row> Rows { get; }
        public Cell Get(int column, uint row)
        {
            Cell cell;
            return _cells.TryGetValue(Address(column, row), out cell) ? cell : null;
        }

        public string Text(Cell cell)
        {
            if (cell == null || cell.CellFormula != null) return string.Empty;
            if (cell.DataType?.Value == CellValues.InlineString) return cell.InlineString?.InnerText ?? string.Empty;
            if (cell.DataType?.Value == CellValues.SharedString)
            {
                int index;
                if (!int.TryParse(cell.CellValue?.Text, NumberStyles.None, CultureInfo.InvariantCulture, out index)
                    || index < 0 || index >= _sharedStrings.Length)
                    throw Invalid("工作表包含无效的共享字符串引用。");
                return _sharedStrings[index].InnerText;
            }
            return cell.DataType?.Value == CellValues.String ? cell.CellValue?.Text ?? string.Empty : string.Empty;
        }

        public string HeaderAbove(int column, uint row)
        {
            if (row <= 1) return string.Empty;
            CellRange[] ranges = _merges.Where(range => range.Contains(column, row - 1)).ToArray();
            if (ranges.Length > 1) throw Invalid("金额表头存在重叠合并区域。");
            if (ranges.Length == 0) return Text(Get(column, row - 1));
            CellRange group = ranges[0];
            // Only an immediately adjacent, horizontal group header is supported.
            return group.Start.Row == row - 1 && group.End.Row == row - 1
                ? Text(Get(group.Start.Column, group.Start.Row)) : string.Empty;
        }

        public bool IsMerged(int column, uint row) =>
            _merges.Any(range => range.Contains(column, row) &&
                (range.Start.Row != range.End.Row || range.Start.Column != range.End.Column));

        public static int Column(Cell cell) => ParseAddress(cell.CellReference?.Value).Column;
        public static string Address(int column, uint row)
        {
            string letters = string.Empty;
            for (int remaining = column; remaining > 0; remaining = (remaining - 1) / 26)
                letters = (char)('A' + (remaining - 1) % 26) + letters;
            return letters + row.ToString(CultureInfo.InvariantCulture);
        }

        private static CellPosition ParseAddress(string address)
        {
            Match match = AddressPattern.Match(address ?? string.Empty);
            if (!match.Success) throw Invalid("单元格地址缺失或无效。");
            int column = 0;
            foreach (char letter in match.Groups["column"].Value) column = column * 26 + letter - 'A' + 1;
            uint row = uint.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture);
            if (column > 16384 || row > 1048576) throw Invalid("单元格地址超出 .xlsx 范围。");
            return new CellPosition(column, row);
        }

        private static ExcelSummaryReadException Invalid(string message) =>
            new ExcelSummaryReadException(ExcelSummaryReadStatus.InvalidWorkbook, message);

        private sealed class CellPosition
        {
            public CellPosition(int column, uint row) { Column = column; Row = row; }
            public int Column { get; }
            public uint Row { get; }
        }

        private sealed class CellRange
        {
            public CellRange(CellPosition start, CellPosition end) { Start = start; End = end; }
            public CellPosition Start { get; }
            public CellPosition End { get; }
            public bool Contains(int column, uint row) =>
                column >= Start.Column && column <= End.Column && row >= Start.Row && row <= End.Row;
        }
    }
}
