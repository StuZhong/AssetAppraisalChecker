using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Facts.Extraction.Conclusions;
using AssetAppraisalChecker.Facts.Models.Conclusions;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.V3
{
    [TestClass]
    public sealed class WordConclusionExtractorTests
    {
        private const string Narrative = "评估值（不含增值税）合计为人民币壹佰肆拾玖万叁仟捌佰元（RMB149.38万元），"
            + "评估值（含增值税）合计为人民币壹佰陆拾壹万捌仟壹佰元（RMB161.81万元）。";

        [TestMethod]
        public void Extract_TwoFiles_Returns36AttributedAmountsAndUnchangedSources()
        {
            using (SyntheticDocxFile report = Report())
            using (SyntheticDocxFile explanation = Explanation())
            {
                byte[] reportHash = Hash(report.FilePath), explanationHash = Hash(explanation.FilePath);
                DocumentReadResult reportDocument = new DocxDocumentReader().Read(report.FilePath);
                DocumentReadResult explanationDocument = new DocxDocumentReader().Read(explanation.FilePath);
                var extractor = new WordConclusionExtractor();
                WordConclusionExtractionResult a = extractor.Extract(reportDocument, "文件A", WordConclusionDocumentKind.Report);
                WordConclusionExtractionResult b = extractor.Extract(explanationDocument, "文件B", WordConclusionDocumentKind.Explanation);
                Assert.IsTrue(a.IsSuccess, a.Message);
                Assert.IsTrue(b.IsSuccess, b.Message);
                CollectionAssert.AreEqual(new[] { WordConclusionTarget.ReportSummary, WordConclusionTarget.ReportChapterTen },
                    a.Conclusions.Select(conclusion => conclusion.Target).ToArray());
                Assert.AreEqual(WordConclusionTarget.ExplanationChapterFour, b.Conclusions.Single().Target);
                Assert.AreEqual(36, a.AmountCount + b.AmountCount);
                foreach (WordConclusion conclusion in a.Conclusions.Concat(b.Conclusions))
                {
                    AssertAttributed(conclusion, conclusion.SourceDocument == "文件A" ? reportDocument : explanationDocument);
                }
                CollectionAssert.AreEqual(reportHash, Hash(report.FilePath));
                CollectionAssert.AreEqual(explanationHash, Hash(explanation.FilePath));
            }
        }

        [TestMethod]
        public void Extract_MergedTwoLevelHeaderAndPageBreakInsideTable_PreservesPhysicalLocations()
        {
            using (SyntheticDocxFile file = Explanation(grouped: true))
            {
                DocumentReadResult document = new DocxDocumentReader().Read(file.FilePath);
                WordConclusionExtractionResult result = new WordConclusionExtractor().Extract(document, "文件B", WordConclusionDocumentKind.Explanation);
                Assert.IsTrue(result.IsSuccess, result.Message);
                WordConclusion conclusion = result.Conclusions.Single();
                AssertAttributed(conclusion, document);
                WordConclusionAmount total = conclusion.Amounts.Single(amount => amount.SourceKind == WordConclusionSourceKind.TableTotalRow
                    && amount.ValueCategory == ValueCategory.AppraisedOriginalValue && amount.TaxBasis == TaxBasis.TaxExcluded);
                Assert.AreEqual(1, total.Location.CellIndex);
                Assert.AreEqual(2, total.Location.LogicalGridStart);
                Assert.AreEqual(3, total.Location.RowIndex);
                Assert.IsTrue(conclusion.Amounts.Where(amount => amount.Location.TableIndex.HasValue)
                    .All(amount => amount.Location.TableIndex == conclusion.TableIndex));
            }
        }

        [TestMethod]
        public void Extract_DifferentArabicAndChineseValues_ExtractsWithoutComparing()
        {
            using (SyntheticDocxFile file = Explanation(narrative: Narrative.Replace("149.38", "149.39")))
            {
                WordConclusionExtractionResult result = Extract(file);
                Assert.IsTrue(result.IsSuccess, result.Message);
                var amounts = result.Conclusions.Single().Amounts;
                Assert.AreEqual(1493900m, amounts.Single(amount => amount.SourceKind == WordConclusionSourceKind.BodyArabic
                    && amount.TaxBasis == TaxBasis.TaxExcluded).NormalizedAmountYuan);
                Assert.AreEqual(1493800m, amounts.Single(amount => amount.SourceKind == WordConclusionSourceKind.BodyChineseFinancial
                    && amount.TaxBasis == TaxBasis.TaxExcluded).NormalizedAmountYuan);
            }
        }

        [TestMethod]
        public void Extract_TocAndCalculationCasesOnly_FailsInsteadOfUsingTheirAmounts()
        {
            using (SyntheticDocxFile file = Create(Paragraph("第四章 评估结论及分析", "TOC"),
                Heading("第三章 成本法评估技术说明"), Paragraph("计算案例：重置成本999万元。"), Paragraph(Narrative), ResultTable(false)))
                AssertFailure(Extract(file), WordConclusionExtractionStatus.TargetNotFound);
        }

        [TestMethod]
        public void Extract_DuplicateTargetHeading_FailsClosed()
        {
            using (SyntheticDocxFile file = Create(Heading("第四章 评估结论及分析"), Paragraph(Narrative), Unit(), ResultTable(false),
                Heading("第四章 评估结论及分析"), Paragraph(Narrative), Unit(), ResultTable(false)))
                AssertFailure(Extract(file), WordConclusionExtractionStatus.AmbiguousTarget);
        }

        [TestMethod]
        [DataRow("missing-uppercase")]
        [DataRow("unknown-tax")]
        [DataRow("original-not-net")]
        public void Extract_UnreliableNarrativeField_FailsWithoutPartialAmounts(string mode)
        {
            string text = mode == "missing-uppercase" ? Narrative.Replace("壹佰陆拾壹万捌仟壹佰元", "无法确认")
                : mode == "unknown-tax" ? Narrative.Replace("（不含增值税）", string.Empty).Replace("（含增值税）", string.Empty)
                : Narrative.Replace("评估值", "评估原值");
            using (SyntheticDocxFile file = Explanation(narrative: text))
                AssertFailure(Extract(file), WordConclusionExtractionStatus.IncompleteAmounts);
        }

        [TestMethod]
        public void Extract_ExtraNarrativeAmount_FailsInsteadOfChoosingOne()
        {
            using (SyntheticDocxFile file = Explanation(narrative: Narrative + "不含税评估净值150万元。"))
                AssertFailure(Extract(file), WordConclusionExtractionStatus.AmbiguousAmounts);
        }

        [TestMethod]
        public void Extract_MissingTableField_FailsWithoutShiftingColumns()
        {
            Table table = ResultTable(false);
            table.Elements<TableRow>().First().Elements<TableCell>().ElementAt(5).RemoveAllChildren<Paragraph>();
            table.Elements<TableRow>().First().Elements<TableCell>().ElementAt(5).Append(Paragraph("备注"));
            using (SyntheticDocxFile file = Create(Heading("第四章 评估结论及分析"), Paragraph(Narrative), Unit(), table))
                AssertFailure(Extract(file), WordConclusionExtractionStatus.IncompleteAmounts);
        }

        [TestMethod]
        public void Extract_MultipleAssetRows_FailsAsUnsupported()
        {
            Table table = ResultTable(false);
            TableRow total = table.Elements<TableRow>().Last();
            table.InsertBefore(AssetRow(), total);
            using (SyntheticDocxFile file = Create(Heading("第四章 评估结论及分析"), Paragraph(Narrative), Unit(), table))
                AssertFailure(Extract(file), WordConclusionExtractionStatus.UnsupportedTableStructure);
        }

        private static void AssertAttributed(WordConclusion conclusion, DocumentReadResult document)
        {
            Assert.AreEqual(12, conclusion.Amounts.Count);
            Assert.AreEqual(2, conclusion.Amounts.Count(amount => amount.SourceKind == WordConclusionSourceKind.BodyArabic));
            Assert.AreEqual(2, conclusion.Amounts.Count(amount => amount.SourceKind == WordConclusionSourceKind.BodyChineseFinancial));
            Assert.AreEqual(4, conclusion.Amounts.Count(amount => amount.SourceKind == WordConclusionSourceKind.TableAssetRow));
            Assert.AreEqual(4, conclusion.Amounts.Count(amount => amount.SourceKind == WordConclusionSourceKind.TableTotalRow));
            foreach (WordConclusionAmount amount in conclusion.Amounts)
            {
                Assert.AreEqual(conclusion.Target, amount.Target);
                Assert.AreEqual(conclusion.SourceDocument, amount.SourceDocument);
                decimal expected = amount.ValueCategory == ValueCategory.AppraisedOriginalValue
                    ? (amount.TaxBasis == TaxBasis.TaxExcluded ? 1991700m : 2157400m)
                    : (amount.TaxBasis == TaxBasis.TaxExcluded ? 1493800m : 1618100m);
                Assert.AreEqual(expected, amount.NormalizedAmountYuan);
                DocumentElement original = document.Elements.Single(element => amount.Location.TableIndex.HasValue
                    ? element.Location.TableIndex == amount.Location.TableIndex && element.Location.RowIndex == amount.Location.RowIndex
                        && element.Location.CellIndex == amount.Location.CellIndex
                    : element.Location.ParagraphIndex == amount.Location.ParagraphIndex);
                Assert.AreEqual(amount.RawText, original.Text.Substring(amount.TextStartIndex, amount.TextLength));
                Assert.IsFalse(string.IsNullOrWhiteSpace(amount.Evidence));
                if (amount.SourceKind == WordConclusionSourceKind.BodyArabic || amount.SourceKind == WordConclusionSourceKind.BodyChineseFinancial)
                    Assert.AreEqual(ValueCategory.AppraisedNetValue, amount.ValueCategory);
            }
        }

        private static WordConclusionExtractionResult Extract(SyntheticDocxFile file) => new WordConclusionExtractor()
            .Extract(new DocxDocumentReader().Read(file.FilePath), "文件B", WordConclusionDocumentKind.Explanation);

        private static void AssertFailure(WordConclusionExtractionResult result, WordConclusionExtractionStatus status)
        {
            Assert.IsFalse(result.IsSuccess, result.Message);
            Assert.AreEqual(status, result.Status, result.Message);
            Assert.AreEqual(0, result.AmountCount);
            Assert.AreEqual(0, result.Conclusions.Count);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Message));
        }

        private static SyntheticDocxFile Report() => Create(Paragraph("资产评估报告摘要\t2", "TOC"),
            Heading("资产评估报告摘要"), Paragraph(Narrative), Unit(), ResultTable(false),
            Heading("资产评估报告正文"), Heading("评估方法", "H2"), Paragraph("成本法案例重置成本999万元。"),
            Paragraph(Narrative), Unit(), ResultTable(false), Heading("评估结论", "H2"),
            Paragraph(Narrative), Unit(), ResultTable(false), Heading("特别事项说明", "H2"), Paragraph(Narrative));

        private static SyntheticDocxFile Explanation(bool grouped = false, string narrative = Narrative) => Create(
            Paragraph("第四章 评估结论及分析\t10", "TOC"), Heading("第三章 成本法评估技术说明"),
            Paragraph("计算案例重置成本999万元。"), Paragraph(Narrative), Unit(), ResultTable(false),
            Heading("第四章 评估结论及分析"), Heading("评估结论及分析", "H2"), Paragraph("（一）评估结论"),
            Paragraph(narrative), Unit(), ResultTable(grouped), Heading("第五章 其他说明"), Paragraph(Narrative));

        private static SyntheticDocxFile Create(params OpenXmlElement[] elements) => SyntheticDocxFile.CreateWithStyles(new[]
        {
            new Style(new StyleName { Val = "heading 1" }) { StyleId = "H1", Type = StyleValues.Paragraph },
            new Style(new StyleName { Val = "heading 2" }) { StyleId = "H2", Type = StyleValues.Paragraph },
            new Style(new StyleName { Val = "toc 1" }) { StyleId = "TOC", Type = StyleValues.Paragraph }
        }, elements);

        private static Paragraph Heading(string text, string style = "H1") => Paragraph(text, style);
        private static Paragraph Unit() => Paragraph("金额单位：人民币万元");
        private static Paragraph Paragraph(string text, string style = null)
        {
            var paragraph = new Paragraph();
            if (style != null) paragraph.ParagraphProperties = new ParagraphProperties(new ParagraphStyleId { Val = style });
            // Real WPS files split labels, Arabic numbers and uppercase amounts across runs.
            for (int index = 0; index < text.Length; index += 3)
                paragraph.Append(new Run(new Text(text.Substring(index, Math.Min(3, text.Length - index))) { Space = SpaceProcessingModeValues.Preserve }));
            return paragraph;
        }

        private static Table ResultTable(bool grouped)
        {
            var table = new Table(new TableGrid(Enumerable.Range(0, 7).Select(_ => new GridColumn()).ToArray()));
            if (grouped)
            {
                table.Append(new TableRow(Cell("序号"), Cell("资产名称"), Cell("评估值（不含税）", 2), Cell("评估值（含税）", 2), Cell("备注")),
                    new TableRow(Cell(string.Empty), Cell(string.Empty), Cell("原值"), Cell("净值"), Cell("原值"), Cell("净值"), Cell(string.Empty)));
            }
            else table.Append(new TableRow(Cell("序号"), Cell("资产名称"), Cell("评估原值（不含税）"), Cell("评估净值（不含税）"),
                Cell("评估原值（含税）"), Cell("评估净值（含税）"), Cell("备注")));
            table.Append(AssetRow(), new TableRow(Cell("合计", 2), Cell("199.17"), Cell("149.38"), Cell("215.74"), Cell("161.81"), Cell(string.Empty)));
            return table;
        }

        private static TableRow AssetRow()
        {
            TableCell label = Cell("虚构资产");
            label.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page }, new Text("A"))));
            return new TableRow(Cell("1"), label, Cell(" 199.17 "), Cell("149.38"), Cell("215.74"), Cell("161.81"), Cell("仅为虚构测试"));
        }

        private static TableCell Cell(string text, int span = 1) => new TableCell(
            new TableCellProperties(new GridSpan { Val = span }), Paragraph(text));

        private static byte[] Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return hash.ComputeHash(stream);
        }
    }
}
