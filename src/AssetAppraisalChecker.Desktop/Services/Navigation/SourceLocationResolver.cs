using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Workflow;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Desktop.Services.Navigation
{
    public sealed class SourceNavigationTarget
    {
        internal SourceNavigationTarget(CheckedDocumentSource source, ReportCheckOccurrence occurrence,
            string containerText, int? offset, int length, bool sensitive, int[] rowCellCounts,
            string[] cellParagraphTexts = null, bool targetRowHasMerge = false,
            bool targetCellOneToOne = false)
        {
            Source = source;
            Occurrence = occurrence;
            ContainerText = containerText;
            Offset = offset;
            Length = length;
            DisplaySensitive = sensitive;
            RowCellCounts = Array.AsReadOnly(rowCellCounts ?? new int[0]);
            CellParagraphTexts = Array.AsReadOnly(cellParagraphTexts ?? new string[0]);
            TargetRowHasMerge = targetRowHasMerge;
            TargetCellOneToOne = targetCellOneToOne;
        }
        public CheckedDocumentSource Source { get; }
        public ReportCheckOccurrence Occurrence { get; }
        public DocumentLocation Location => Occurrence.Location;
        public string ContainerText { get; }
        public int? Offset { get; }
        public int Length { get; }
        public bool DisplaySensitive { get; }
        public System.Collections.Generic.IReadOnlyList<int> RowCellCounts { get; }
        public IReadOnlyList<string> CellParagraphTexts { get; }
        public bool TargetRowHasMerge { get; }
        public bool TargetCellOneToOne { get; }
        public string SpanText => Offset.HasValue ? ContainerText.Substring(Offset.Value, Length) : string.Empty;
    }

    public sealed class SourceLocationResolver
    {
        private static readonly Regex Span = new Regex(
            "(?:原文范围|证据范围)：Start=(?<start>[0-9]+)，Length=(?<length>[0-9]+)", RegexOptions.CultureInvariant);
        private static readonly Regex Offset = new Regex(
            "(?:元素内字符偏移（从 0 开始）|字符偏移)=(?<start>[0-9]+)(?:；长度=(?<length>[0-9]+))?", RegexOptions.CultureInvariant);
        private static readonly Regex SeparateLength = new Regex(
            "\\A长度=(?<length>[0-9]+)\\z", RegexOptions.CultureInvariant);

        public SourceNavigationTarget Resolve(CheckedDocumentSource source, ReportCheckOccurrence occurrence)
        {
            if (source == null || occurrence == null) return null;
            DocumentLocation location = occurrence.Location;
            if (location.DocumentPart != DocumentPart.MainBody) return null;
            // Document extraction stores one aggregate element per physical table cell,
            // while formatting findings also carry a paragraph index within that cell.
            var matches = source.Document.Elements.Where(e => location.TableIndex.HasValue
                ? SameCell(e.Location, location)
                : SameLocation(e.Location, location)).ToList();
            if (matches.Count != 1) return null;
            string text = matches[0].Text;
            OpenXmlElement anchor;
            int[] counts = null;
            string[] cellParagraphTexts = null;
            bool targetRowHasMerge = false;
            bool targetCellOneToOne = false;
            // Parse a verified read-only memory snapshot, not the editor's live file.
            using (var snapshot = source.ReadVerifiedSnapshot())
            using (var package = WordprocessingDocument.Open(snapshot, false))
            {
                Body body = package.MainDocumentPart?.Document?.Body;
                if (body == null) return null;
                if (location.TableIndex.HasValue)
                {
                    if (!location.RowIndex.HasValue || !location.CellIndex.HasValue) return null;
                    Table table = body.Elements<Table>().ElementAtOrDefault(location.TableIndex.Value);
                    if (table == null) return null;
                    var rows = table.Elements<TableRow>().ToList();
                    counts = rows.Select(r => r.Elements<TableCell>().Count()).ToArray();
                    TableRow row = rows.ElementAtOrDefault(location.RowIndex.Value);
                    anchor = row?.Elements<TableCell>()
                        .ElementAtOrDefault(location.CellIndex.Value);
                    if (anchor == null || anchor.Descendants<Table>().Any()) return null;
                    targetRowHasMerge = RowHasMerge(row);
                    targetCellOneToOne = CellHasDirectColumn(row, location.CellIndex.Value);
                    cellParagraphTexts = ((TableCell)anchor).Elements<Paragraph>()
                        .Select(VisibleText).ToArray();
                }
                else
                {
                    if (!location.ParagraphIndex.HasValue) return null;
                    anchor = body.Elements<Paragraph>().ElementAtOrDefault(location.ParagraphIndex.Value);
                }
                if (anchor == null) return null;
                if (location.TableIndex.HasValue && location.ParagraphIndex.HasValue)
                {
                    if (location.ParagraphIndex.Value < 0 || location.ParagraphIndex.Value >= cellParagraphTexts.Length)
                        return null;
                    string paragraphText = cellParagraphTexts[location.ParagraphIndex.Value];
                    if (string.IsNullOrEmpty(occurrence.RawText)
                        ? paragraphText.Length != 0
                        : !paragraphText.Contains(occurrence.RawText)) return null;
                }
                bool sensitive = anchor.Descendants().Any(e => e is DeletedText || e is InsertedRun || e is Vanish ||
                    e is FieldCode || e is FieldChar || e is SimpleField);
                string raw = occurrence.RawText;
                if (string.IsNullOrEmpty(raw))
                    return location.TableIndex.HasValue && text.Length == 0
                        ? new SourceNavigationTarget(source, occurrence, text, null, 0, sensitive,
                            counts, cellParagraphTexts, targetRowHasMerge, targetCellOneToOne)
                        : null;
                if (sensitive)
                {
                    // Deleted text is absent from Document's visible text; verify it in XML instead.
                    bool hasOriginal = text.Contains(raw) || anchor.Descendants<DeletedRun>().Any(revision =>
                        string.Concat(revision.Descendants<DeletedText>().Select(t => t.Text)).Contains(raw));
                    if (!hasOriginal) return null;
                    return new SourceNavigationTarget(source, occurrence, text, null, 0, true,
                        counts, cellParagraphTexts, targetRowHasMerge, targetCellOneToOne);
                }
                if (!text.Contains(raw)) return null;
                if (location.TableIndex.HasValue && location.ParagraphIndex.HasValue)
                    return new SourceNavigationTarget(source, occurrence, text, null, 0, sensitive,
                        counts, cellParagraphTexts, targetRowHasMerge, targetCellOneToOne);
                // Some existing producers emit offset and length as separate evidence entries.
                // Never substitute the whole container's length for that explicit span length.
                var lengths = occurrence.Evidence.Where(e => e.StartsWith("长度=", StringComparison.Ordinal)).ToList();
                int? separateLength = null;
                if (lengths.Count > 0)
                {
                    int value;
                    Match lengthMatch = SeparateLength.Match(lengths[0]);
                    if (lengths.Count != 1 || !lengthMatch.Success || !int.TryParse(lengthMatch.Groups["length"].Value,
                        NumberStyles.None, CultureInfo.InvariantCulture, out value) || value <= 0) return null;
                    separateLength = value;
                }
                foreach (string evidence in occurrence.Evidence)
                {
                    Match span = Span.Match(evidence);
                    if (!span.Success) span = Offset.Match(evidence);
                    if (!span.Success) continue;
                    int start;
                    int length;
                    if (!int.TryParse(span.Groups["start"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out start)) return null;
                    if (span.Groups["length"].Success)
                    {
                        if (!int.TryParse(span.Groups["length"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out length) ||
                            separateLength.HasValue && separateLength.Value != length) return null;
                    }
                    else if (separateLength.HasValue) length = separateLength.Value;
                    else
                    {
                        if (evidence.StartsWith("原始字符=\\t；Unicode=U+0009；", StringComparison.Ordinal) &&
                            start < text.Length && text[start] == '\t') length = 1;
                        else length = raw.Length;
                    }
                    if (start < 0 || length <= 0 || start > text.Length - length) return null;
                    string fragment = text.Substring(start, length);
                    if (raw != text && fragment != raw && !raw.Contains(fragment)) return null;
                    return new SourceNavigationTarget(source, occurrence, text, start, length, false,
                        counts, cellParagraphTexts, targetRowHasMerge, targetCellOneToOne);
                }
                int first = text.IndexOf(raw, StringComparison.Ordinal);
                int second = text.IndexOf(raw, first + 1, StringComparison.Ordinal);
                return new SourceNavigationTarget(source, occurrence, text, second < 0 ? (int?)first : null,
                    second < 0 ? raw.Length : 0, false, counts, cellParagraphTexts,
                    targetRowHasMerge, targetCellOneToOne);
            }
        }

        private static bool SameLocation(DocumentLocation a, DocumentLocation b) =>
            a.DocumentPart == b.DocumentPart && a.ParagraphIndex == b.ParagraphIndex &&
            a.TableIndex == b.TableIndex && a.RowIndex == b.RowIndex && a.CellIndex == b.CellIndex;

        private static bool SameCell(DocumentLocation a, DocumentLocation b) =>
            a.DocumentPart == b.DocumentPart && a.TableIndex == b.TableIndex &&
            a.RowIndex == b.RowIndex && a.CellIndex == b.CellIndex && !a.ParagraphIndex.HasValue;

        private static bool RowHasMerge(TableRow row)
        {
            if (row == null) return true;
            return row.Elements<TableCell>().Any(cell =>
            {
                TableCellProperties properties = cell.TableCellProperties;
                return properties?.VerticalMerge != null || properties?.HorizontalMerge != null ||
                    (properties?.GridSpan != null &&
                        (properties.GridSpan.Val == null || properties.GridSpan.Val.Value != 1));
            }) ||
                row.TableRowProperties?.ChildElements.Any(element =>
                    element.LocalName == "gridBefore" || element.LocalName == "gridAfter") == true;
        }

        private static bool CellHasDirectColumn(TableRow row, int cellIndex)
        {
            if (row == null || row.TableRowProperties?.ChildElements.Any(element =>
                element.LocalName == "gridBefore" || element.LocalName == "gridAfter") == true) return false;
            var cells = row.Elements<TableCell>().ToList();
            if (cellIndex < 0 || cellIndex >= cells.Count) return false;
            for (int i = 0; i <= cellIndex; i++)
            {
                TableCellProperties properties = cells[i].TableCellProperties;
                if (properties?.HorizontalMerge != null || properties?.GridSpan != null &&
                    (properties.GridSpan.Val == null || properties.GridSpan.Val.Value != 1)) return false;
                if (i == cellIndex && properties?.VerticalMerge != null) return false;
            }
            return true;
        }

        private static string VisibleText(Paragraph paragraph)
        {
            var text = new StringBuilder();
            foreach (OpenXmlElement element in paragraph.Descendants())
            {
                if (element is Text value) text.Append(value.Text);
                else if (element is TabChar) text.Append('\t');
                else if (element is Break || element is CarriageReturn) text.Append('\n');
            }
            return text.ToString();
        }
    }
}
