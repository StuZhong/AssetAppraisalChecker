using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class DocumentFormattingSnapshotReader
    {
        public DocumentFormattingSnapshot Read(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("DOCX 路径不能为空。", nameof(filePath));
            }
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("找不到待解析的 DOCX 文件。", filePath);
            }

            using (FileStream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            using (WordprocessingDocument document = WordprocessingDocument.Open(stream, false))
            {
                MainDocumentPart mainPart = document.MainDocumentPart;
                if (mainPart?.Document?.Body == null)
                {
                    throw new InvalidDataException("DOCX 缺少有效正文结构。");
                }

                Styles styles = mainPart.StyleDefinitionsPart?.Styles;
                var catalog = new StyleCatalog(styles);
                var theme = new ThemeFontResolver(mainPart.ThemePart);
                var resolver = new EffectiveStyleResolver(catalog, theme, styles?.DocDefaults);
                var traversal = new SnapshotTraversal(resolver);
                traversal.Process(mainPart.Document.Body);

                return new DocumentFormattingSnapshot(
                    traversal.Paragraphs,
                    traversal.Tables,
                    catalog.Metadata,
                    resolver.DefaultsSnapshot,
                    theme.Metadata);
            }
        }

        private sealed class SnapshotTraversal
        {
            private readonly EffectiveStyleResolver resolver;
            private readonly List<ParagraphFormattingSnapshot> paragraphs =
                new List<ParagraphFormattingSnapshot>();
            private readonly List<TableFormattingContext> tables =
                new List<TableFormattingContext>();
            private int paragraphIndex;
            private int bodyParagraphIndex;
            private int tableIndex;

            public SnapshotTraversal(EffectiveStyleResolver resolver)
            {
                this.resolver = resolver;
            }

            public IReadOnlyList<ParagraphFormattingSnapshot> Paragraphs => paragraphs.AsReadOnly();
            public IReadOnlyList<TableFormattingContext> Tables => tables.AsReadOnly();

            public void Process(Body body)
            {
                foreach (OpenXmlElement block in body.ChildElements)
                {
                    Paragraph paragraph = block as Paragraph;
                    if (paragraph != null)
                    {
                        AddParagraph(
                            paragraph,
                            new FormattingParagraphLocation(
                                paragraphIndex++, bodyParagraphIndex++,
                                null, null, null, null),
                            null);
                        continue;
                    }

                    Table table = block as Table;
                    if (table != null)
                    {
                        ProcessTable(table);
                    }
                }
            }

            private void ProcessTable(Table table)
            {
                int currentTableIndex = tableIndex++;
                string tableStyleId = table.TableProperties?.TableStyle?.Val?.Value ?? string.Empty;
                var context = new TableFormattingContext(
                    currentTableIndex,
                    tableStyleId,
                    FormattingResolutionStatus.NotImplemented);
                tables.Add(context);

                int rowIndex = 0;
                foreach (TableRow row in table.Elements<TableRow>())
                {
                    int cellIndex = 0;
                    foreach (TableCell cell in row.Elements<TableCell>())
                    {
                        int cellParagraphIndex = 0;
                        foreach (OpenXmlElement block in cell.ChildElements)
                        {
                            Paragraph paragraph = block as Paragraph;
                            if (paragraph != null)
                            {
                                AddParagraph(
                                    paragraph,
                                    new FormattingParagraphLocation(
                                        paragraphIndex++, null,
                                        currentTableIndex, rowIndex, cellIndex,
                                        cellParagraphIndex++),
                                    context);
                            }
                            else
                            {
                                Table nestedTable = block as Table;
                                if (nestedTable != null)
                                {
                                    ProcessTable(nestedTable);
                                }
                            }
                        }

                        cellIndex++;
                    }

                    rowIndex++;
                }
            }

            private void AddParagraph(
                Paragraph paragraph,
                FormattingParagraphLocation location,
                TableFormattingContext tableContext)
            {
                ParagraphResolution paragraphResolution = resolver.ResolveParagraph(paragraph);
                var runs = new List<RunFormattingSnapshot>();
                int runIndex = 0;
                foreach (Run run in paragraph.Descendants<Run>())
                {
                    string text = string.Concat(run.Descendants<Text>().Select(item => item.Text));
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    runs.Add(resolver.ResolveRun(
                        run,
                        runIndex++,
                        text,
                        paragraphResolution,
                        run.Ancestors<Hyperlink>().Any(),
                        run.Descendants<FieldCode>().Any() ||
                            run.Ancestors<SimpleField>().Any()));
                }

                string paragraphText = string.Concat(
                    paragraph.Descendants<Text>().Select(item => item.Text));
                paragraphs.Add(paragraphResolution.ParagraphValues.CreateSnapshot(
                    location,
                    Preview(paragraphText),
                    paragraphResolution.StyleId,
                    paragraphResolution.StyleResolution,
                    tableContext,
                    runs));
            }

            private static string Preview(string text)
            {
                const int maxLength = 160;
                string value = text ?? string.Empty;
                return value.Length <= maxLength
                    ? value
                    : value.Substring(0, maxLength) + "…";
            }
        }
    }
}
