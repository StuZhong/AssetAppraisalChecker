using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Document
{
    internal sealed class OpenXmlDocumentExtractor
    {
        private readonly WordprocessingDocument _document;
        private readonly HeadingStyleResolver _headingStyleResolver;
        private readonly SortedDictionary<int, string> _headingByLevel;
        private readonly List<ExtractedDocumentElement> _elements;
        private int _documentOrderIndex;
        private int _paragraphIndex;
        private int _tableIndex;

        public OpenXmlDocumentExtractor(WordprocessingDocument document)
        {
            _document = document;
            _headingStyleResolver = new HeadingStyleResolver(document.MainDocumentPart);
            _headingByLevel = new SortedDictionary<int, string>();
            _elements = new List<ExtractedDocumentElement>();
        }

        public DocumentReadResult Extract()
        {
            Body body = _document.MainDocumentPart.Document.Body;
            foreach (OpenXmlElement block in body.ChildElements)
            {
                var paragraph = block as Paragraph;
                if (paragraph != null)
                {
                    ExtractParagraph(paragraph);
                    continue;
                }

                var table = block as Table;
                if (table != null)
                {
                    ExtractTable(table);
                }
            }

            IReadOnlyList<DocumentElement> elements = NearbyContextBuilder.Build(_elements);
            return DocumentReadResult.Succeeded(elements, _paragraphIndex, _tableIndex);
        }

        private void ExtractParagraph(Paragraph paragraph)
        {
            string styleId;
            int? headingLevel = _headingStyleResolver.GetHeadingLevel(paragraph, out styleId);
            string text = OpenXmlTextExtractor.ExtractParagraphText(paragraph);

            if (headingLevel.HasValue && !string.IsNullOrWhiteSpace(text))
            {
                UpdateHeadingPath(headingLevel.Value, text);
            }

            var location = new DocumentLocation(
                DocumentPart.MainBody,
                _paragraphIndex,
                null,
                null,
                null,
                CurrentHeadingPath());

            _elements.Add(new ExtractedDocumentElement(
                headingLevel.HasValue ? DocumentElementType.Heading : DocumentElementType.Paragraph,
                text,
                location,
                _documentOrderIndex,
                styleId,
                CountExplicitPageBreaks(paragraph),
                GetSectionBreakKind(paragraph)));

            _paragraphIndex++;
            _documentOrderIndex++;
        }

        private void ExtractTable(Table table)
        {
            int? tableGridColumnCount = GetTableGridColumnCount(table);
            int rowIndex = 0;
            foreach (TableRow row in table.Elements<TableRow>())
            {
                List<TableCell> cells = row.Elements<TableCell>().ToList();
                IReadOnlyList<LogicalCellPosition> logicalPositions =
                    BuildLogicalCellPositions(cells, tableGridColumnCount);
                int cellIndex = 0;
                foreach (TableCell cell in cells)
                {
                    LogicalCellPosition logicalPosition = logicalPositions[cellIndex];
                    var location = new DocumentLocation(
                        DocumentPart.MainBody,
                        null,
                        _tableIndex,
                        rowIndex,
                        cellIndex,
                        CurrentHeadingPath(),
                        tableGridColumnCount,
                        logicalPosition.Start,
                        logicalPosition.Span);

                    _elements.Add(new ExtractedDocumentElement(
                        DocumentElementType.TableCell,
                        OpenXmlTextExtractor.ExtractCellText(cell),
                        location,
                        _documentOrderIndex,
                        string.Empty,
                        CountExplicitPageBreaks(cell),
                        DocumentSectionBreakKind.None));

                    cellIndex++;
                    _documentOrderIndex++;
                }

                rowIndex++;
            }

            _tableIndex++;
        }

        private static int CountExplicitPageBreaks(OpenXmlElement element)
        {
            return element
                .Descendants<Break>()
                .Count(item => item.Type != null && item.Type.Value == BreakValues.Page);
        }

        private static DocumentSectionBreakKind GetSectionBreakKind(Paragraph paragraph)
        {
            SectionProperties properties = paragraph
                ?.ParagraphProperties
                ?.GetFirstChild<SectionProperties>();
            if (properties == null)
            {
                return DocumentSectionBreakKind.None;
            }

            SectionType sectionType = properties.GetFirstChild<SectionType>();
            if (sectionType?.Val == null || sectionType.Val.Value == SectionMarkValues.NextPage)
            {
                return DocumentSectionBreakKind.NextPage;
            }

            SectionMarkValues value = sectionType.Val.Value;
            if (value == SectionMarkValues.Continuous)
            {
                return DocumentSectionBreakKind.Continuous;
            }

            if (value == SectionMarkValues.EvenPage)
            {
                return DocumentSectionBreakKind.EvenPage;
            }

            if (value == SectionMarkValues.OddPage)
            {
                return DocumentSectionBreakKind.OddPage;
            }

            if (value == SectionMarkValues.NextColumn)
            {
                return DocumentSectionBreakKind.NextColumn;
            }

            return DocumentSectionBreakKind.Unknown;
        }

        private static int? GetTableGridColumnCount(Table table)
        {
            TableGrid grid = table.GetFirstChild<TableGrid>();
            int count = grid == null ? 0 : grid.Elements<GridColumn>().Count();
            return count > 0 ? count : (int?)null;
        }

        private static IReadOnlyList<LogicalCellPosition> BuildLogicalCellPositions(
            IReadOnlyList<TableCell> cells,
            int? tableGridColumnCount)
        {
            var spans = new List<int?>(cells.Count);
            long totalSpan = 0;
            foreach (TableCell cell in cells)
            {
                Int32Value spanValue = cell.TableCellProperties?.GridSpan?.Val;
                long span = spanValue == null ? 1L : spanValue.Value;
                if (span <= 0 || span > int.MaxValue)
                {
                    spans.Add(null);
                    continue;
                }

                spans.Add((int)span);
                totalSpan += span;
            }

            bool rowIsMappable =
                tableGridColumnCount.HasValue &&
                spans.All(span => span.HasValue) &&
                totalSpan == tableGridColumnCount.Value;
            var result = new List<LogicalCellPosition>(cells.Count);
            int logicalStart = 0;
            foreach (int? span in spans)
            {
                result.Add(new LogicalCellPosition(
                    rowIsMappable ? logicalStart : (int?)null,
                    span));
                if (rowIsMappable && span.HasValue)
                {
                    logicalStart += span.Value;
                }
            }

            return result;
        }

        private sealed class LogicalCellPosition
        {
            public LogicalCellPosition(int? start, int? span)
            {
                Start = start;
                Span = span;
            }

            public int? Start { get; }

            public int? Span { get; }
        }

        private void UpdateHeadingPath(int level, string text)
        {
            foreach (int existingLevel in _headingByLevel.Keys.Where(key => key >= level).ToList())
            {
                _headingByLevel.Remove(existingLevel);
            }

            _headingByLevel[level] = text;
        }

        private IReadOnlyList<string> CurrentHeadingPath()
        {
            return _headingByLevel.OrderBy(item => item.Key).Select(item => item.Value).ToList();
        }
    }
}
