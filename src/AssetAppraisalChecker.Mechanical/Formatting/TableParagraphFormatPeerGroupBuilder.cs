using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    // Shared structural table peers for paragraph formatting and font/size comparisons.
    internal sealed class TableParagraphFormatPeerGroupBuilder
    {
        private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private readonly Dictionary<string, CellContext> cells = new Dictionary<string, CellContext>(StringComparer.Ordinal);
        private int nextTableIndex;

        public TableParagraphFormatPeerGroupBuilder(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (WordprocessingDocument document = WordprocessingDocument.Open(stream, false))
            {
                Body body = document.MainDocumentPart?.Document?.Body;
                if (body == null) return;
                foreach (Table table in body.Elements<Table>()) ReadTable(table);
            }
        }

        public IReadOnlyList<FormattingPeerParagraph> GetLocalPeers(
            FormattingPeerParagraph current, IReadOnlyList<FormattingPeerParagraph> paragraphs)
        {
            CellContext context = Find(current);
            if (context == null || !context.Comparable) return Array.Empty<FormattingPeerParagraph>();
            int row = current.Paragraph.Location.RowIndex.Value;
            // Locality is measured in rows, not flattened paragraphs: wide tables must not
            // borrow other columns to reach the minimum peer count.
            return paragraphs.Where(item => !ReferenceEquals(item, current))
                .Where(item => string.Equals(item.GroupKey, current.GroupKey, StringComparison.Ordinal))
                .Where(item => item.Paragraph.Location.CellParagraphIndex == current.Paragraph.Location.CellParagraphIndex)
                .Where(item => Matches(context, Find(item)))
                .Where(item => item.Paragraph.Location.RowIndex != row)
                .Where(item => Math.Abs(item.Paragraph.Location.RowIndex.Value - row) <= FormattingPeerGroupBuilder.LocalParagraphDistance)
                .OrderBy(item => Math.Abs(item.Paragraph.Location.RowIndex.Value - row))
                .ThenBy(item => item.Paragraph.Location.ParagraphIndex)
                .GroupBy(item => item.Paragraph.Location.RowIndex.Value)
                .Select(group => group.First())
                .Take(FormattingPeerGroupBuilder.MaximumLocalPeers).ToList().AsReadOnly();
        }

        private static bool Matches(CellContext left, CellContext right)
        {
            return right != null && right.Comparable && left.Table == right.Table &&
                left.Column == right.Column && left.Span == right.Span &&
                string.Equals(left.Role, right.Role, StringComparison.Ordinal) &&
                string.Equals(left.Layout, right.Layout, StringComparison.Ordinal);
        }

        private CellContext Find(FormattingPeerParagraph paragraph)
        {
            FormattingParagraphLocation location = paragraph.Paragraph.Location;
            CellContext result;
            return location.TableIndex.HasValue && location.RowIndex.HasValue && location.CellIndex.HasValue &&
                cells.TryGetValue(Key(location.TableIndex.Value, location.RowIndex.Value, location.CellIndex.Value), out result)
                ? result : null;
        }

        private void ReadTable(Table table)
        {
            // Match DocumentFormattingSnapshotReader's direct-child, nested-table preorder.
            int tableIndex = nextTableIndex++;
            int rowIndex = 0;
            foreach (TableRow row in table.Elements<TableRow>())
            {
                List<TableCell> rowCells = row.Elements<TableCell>().ToList();
                string role = RowRole(row, rowCells, rowIndex);
                List<int> spans = rowCells.Select(Span).ToList();
                bool rowKnown = spans.All(span => span > 0) &&
                    !Has(row.TableRowProperties, "gridBefore") && !Has(row.TableRowProperties, "gridAfter") &&
                    !rowCells.Any(cell => Has(cell.TableCellProperties, "hMerge"));
                string layout = string.Join(";", rowCells.Select((cell, index) =>
                    spans[index].ToString(CultureInfo.InvariantCulture) + ":" +
                    (Has(cell.TableCellProperties, "vMerge") ? "vertical" : "ordinary") + ":" +
                    (cell.Elements<Table>().Any() ? "nested" : "plain")));
                int column = 0;
                for (int cellIndex = 0; cellIndex < rowCells.Count; cellIndex++)
                {
                    TableCell cell = rowCells[cellIndex];
                    cells.Add(Key(tableIndex, rowIndex, cellIndex), new CellContext
                    {
                        Table = tableIndex, Column = column, Span = spans[cellIndex], Role = role, Layout = layout,
                        Comparable = rowKnown && role != "Unknown" &&
                            !Has(cell.TableCellProperties, "vMerge") && !cell.Elements<Table>().Any()
                    });
                    column += Math.Max(1, spans[cellIndex]);
                    foreach (Table nested in cell.Elements<Table>()) ReadTable(nested);
                }
                rowIndex++;
            }
        }

        private static string RowRole(TableRow row, List<TableCell> rowCells, int rowIndex)
        {
            string[] labels = rowCells.Select(cell => string.Concat(cell.Elements<Paragraph>()
                .SelectMany(paragraph => paragraph.Descendants<Text>()).Select(text => text.Text)))
                .Select(text => string.Concat(text.Where(character => !char.IsWhiteSpace(character))).TrimEnd('：', ':'))
                .ToArray();
            bool total = labels.Any(text => text == "合计" || text == "总计");
            bool subtotal = labels.Any(text => text == "小计");
            if (total && subtotal) return "Unknown";
            if (total) return "Total";
            if (subtotal) return "Subtotal";
            if (Has(row.TableRowProperties, "tblHeader")) return "Header";
            if (labels.Any(text => text.StartsWith("合计", StringComparison.Ordinal) ||
                text.StartsWith("小计", StringComparison.Ordinal) || text.StartsWith("总计", StringComparison.Ordinal))) return "Unknown";
            string[] headers = { "序号", "项目名称", "资产名称", "名称", "单位", "数量", "金额", "评估原值", "评估净值", "账面原值", "账面净值" };
            if (labels.Count(text => headers.Contains(text, StringComparer.Ordinal)) >= 2) return "Header";
            // Unmarked leading rows may be headers. Isolate rather than infer from values/format.
            return rowIndex == 0 ? "LeadingRow" : "Detail";
        }

        private static int Span(TableCell cell)
        {
            OpenXmlElement element = cell.TableCellProperties?.ChildElements.FirstOrDefault(item => item.LocalName == "gridSpan");
            if (element == null) return 1;
            int value;
            return int.TryParse(element.GetAttribute("val", WordNamespace).Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out value) && value > 0 ? value : 0;
        }

        private static bool Has(OpenXmlElement element, string name) =>
            element != null && element.ChildElements.Any(child => child.LocalName == name);

        private static string Key(int table, int row, int cell) => table + ":" + row + ":" + cell;

        private sealed class CellContext
        {
            public int Table;
            public int Column;
            public int Span;
            public string Role;
            public string Layout;
            public bool Comparable;
        }
    }
}
