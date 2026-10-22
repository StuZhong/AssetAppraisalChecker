using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AssetAppraisalChecker.Application.V3;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Document.Excel;
using AssetAppraisalChecker.Facts.Extraction.Conclusions;
using AssetAppraisalChecker.Facts.Models.Conclusions;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Tests.V3
{
    [TestClass]
    public sealed class V3MoneyComparisonServiceTests
    {
        private static readonly decimal[] RawAmounts = { 199.1727m, 149.3795m, 215.742m, 161.8065m };
        private static readonly string[] DisplayAmounts = { "199.17", "149.38", "215.74", "161.81" };
        private static readonly string[] Headers =
        {
            "评估原值（不含税）", "评估净值（不含税）", "评估原值（含税）", "评估净值（含税）"
        };
        private readonly V3MoneyComparisonService _service = new V3MoneyComparisonService();

        [TestMethod]
        public void Check_ThreeConclusions_Returns36ConsistentAmountsWithBothSources()
        {
            using (var files = new SyntheticInputs())
            {
                V3MoneyComparisonResult result = Check(files);

                AssertAllConsistent(result);
                foreach (WordConclusionTarget target in Enum.GetValues(typeof(WordConclusionTarget)))
                {
                    V3MoneyComparisonItem[] items = result.Items.Where(item => item.Target == target).ToArray();
                    Assert.AreEqual(12, items.Length);
                    Assert.AreEqual(2, items.Count(item => item.SourceKind == WordConclusionSourceKind.BodyArabic));
                    Assert.AreEqual(2, items.Count(item => item.SourceKind == WordConclusionSourceKind.BodyChineseFinancial));
                    Assert.AreEqual(4, items.Count(item => item.SourceKind == WordConclusionSourceKind.TableAssetRow));
                    Assert.AreEqual(4, items.Count(item => item.SourceKind == WordConclusionSourceKind.TableTotalRow));
                }
                foreach (V3MoneyComparisonItem item in result.Items)
                {
                    int field = FieldIndex(item.ValueCategory, item.TaxBasis);
                    Assert.AreEqual(RawAmounts[field], item.ExcelAmount.RawAmountTenThousandYuan);
                    Assert.AreEqual(decimal.Parse(DisplayAmounts[field], CultureInfo.InvariantCulture) * 10000m,
                        item.ExcelComparisonAmountYuan.Value);
                    Assert.AreEqual(item.WordAmount.NormalizedAmountYuan, item.ExcelComparisonAmountYuan.Value);
                    Assert.AreEqual("资产评估汇总表", item.ExcelAmount.WorksheetName);
                    Assert.AreEqual(((char)('C' + field)).ToString()
                        + (item.SourceKind == WordConclusionSourceKind.TableAssetRow ? "6" : "7"), item.ExcelAmount.CellAddress);
                    Assert.AreEqual(item.SourceKind == WordConclusionSourceKind.TableAssetRow
                        ? ExcelSummaryRowKind.Asset : ExcelSummaryRowKind.Total, item.ExcelAmount.RowKind);
                    Assert.AreEqual(item.Target, item.WordAmount.Target);
                    Assert.AreEqual(item.SourceKind, item.WordAmount.SourceKind);
                    Assert.AreEqual(item.TaxBasis, item.WordAmount.TaxBasis);
                    Assert.AreEqual(item.ValueCategory, item.WordAmount.ValueCategory);
                    Assert.AreEqual(item.Target == WordConclusionTarget.ExplanationChapterFour
                        ? files.Explanation.FilePath : files.Report.FilePath, item.WordSourceDocument);
                    Assert.AreEqual(files.WorkbookPath, item.ExcelSourceDocument);
                    Assert.IsNotNull(item.WordAmount.Location);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(item.WordAmount.RawText));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(item.WordAmount.Evidence));
                }
            }
        }

        [TestMethod]
        public void Check_AssetAndTotalHaveDifferentValues_UsesExplicitRowKindAndBodyUsesTotal()
        {
            using (var files = new SyntheticInputs(distinctTotals: true))
            {
                V3MoneyComparisonResult result = Check(files);

                AssertAllConsistent(result);
                foreach (V3MoneyComparisonItem item in result.Items)
                {
                    int field = FieldIndex(item.ValueCategory, item.TaxBasis);
                    decimal expected = decimal.Parse(DisplayAmounts[field], CultureInfo.InvariantCulture);
                    if (item.SourceKind != WordConclusionSourceKind.TableAssetRow) expected += 1m;
                    Assert.AreEqual(expected * 10000m, item.ExcelComparisonAmountYuan.Value);
                }
            }
        }

        [TestMethod]
        public void Check_OneAssetTableAmountChanged_FindsOnlyThatConflictWithD6Evidence()
        {
            using (var files = new SyntheticInputs(firstAssetNet: "149.83"))
            {
                V3MoneyComparisonResult result = Check(files);

                Assert.AreEqual(36, result.TotalCount);
                Assert.AreEqual(35, result.ConsistentCount);
                Assert.AreEqual(1, result.InconsistentCount);
                Assert.AreEqual(0, result.IndeterminateCount);
                V3MoneyComparisonItem conflict = result.Items.Single(item => item.Status == V3MoneyComparisonStatus.Inconsistent);
                Assert.AreEqual(WordConclusionTarget.ReportSummary, conflict.Target);
                Assert.AreEqual(WordConclusionSourceKind.TableAssetRow, conflict.SourceKind);
                Assert.AreEqual(ValueCategory.AppraisedNetValue, conflict.ValueCategory);
                Assert.AreEqual(TaxBasis.TaxExcluded, conflict.TaxBasis);
                Assert.AreEqual("149.83", conflict.WordAmount.RawText);
                Assert.AreEqual(1498300m, conflict.WordAmount.NormalizedAmountYuan);
                Assert.AreEqual(149.3795m, conflict.ExcelAmount.RawAmountTenThousandYuan);
                Assert.AreEqual(1493800m, conflict.ExcelComparisonAmountYuan.Value);
                Assert.AreEqual(4500m, conflict.DifferenceYuan.Value);
                Assert.AreEqual("D6", conflict.ExcelAmount.CellAddress);
                Assert.IsTrue(conflict.WordAmount.Location.TableIndex.HasValue);
                Assert.IsTrue(conflict.WordAmount.Location.RowIndex.HasValue);
                Assert.IsTrue(conflict.WordAmount.Location.CellIndex.HasValue);
                Assert.IsFalse(string.IsNullOrWhiteSpace(conflict.Reason));
            }
        }

        [TestMethod]
        public void Check_ChineseFinancialAmounts_UseReportPrecisionRatherThanRawExcelCache()
        {
            using (var files = new SyntheticInputs())
            {
                V3MoneyComparisonResult result = Check(files);
                V3MoneyComparisonItem[] chinese = result.Items.Where(item =>
                    item.SourceKind == WordConclusionSourceKind.BodyChineseFinancial).ToArray();

                Assert.AreEqual(6, chinese.Length);
                foreach (V3MoneyComparisonItem item in chinese)
                {
                    Assert.AreEqual(V3MoneyComparisonStatus.Consistent, item.Status);
                    Assert.AreEqual(ValueCategory.AppraisedNetValue, item.ValueCategory);
                    Assert.AreEqual(item.TaxBasis == TaxBasis.TaxExcluded
                        ? "壹佰肆拾玖万叁仟捌佰元" : "壹佰陆拾壹万捌仟壹佰元", item.WordAmount.RawText.Replace("人民币", string.Empty));
                    Assert.AreNotEqual(item.ExcelAmount.RawAmountTenThousandYuan * 10000m,
                        item.WordAmount.NormalizedAmountYuan);
                    Assert.AreEqual(item.WordAmount.NormalizedAmountYuan, item.ExcelComparisonAmountYuan.Value);
                    Assert.AreEqual(0m, item.DifferenceYuan.Value);
                }
            }
        }

        [TestMethod]
        public void Check_OneYuanBodyDifference_DoesNotApplyV1DefaultTolerance()
        {
            using (var files = new SyntheticInputs(firstBodyNet: "149.3801"))
            {
                V3MoneyComparisonResult result = Check(files);

                Assert.AreEqual(35, result.ConsistentCount);
                Assert.AreEqual(1, result.InconsistentCount);
                Assert.AreEqual(0, result.IndeterminateCount);
                V3MoneyComparisonItem conflict = result.Items.Single(item => item.Status == V3MoneyComparisonStatus.Inconsistent);
                Assert.AreEqual(WordConclusionSourceKind.BodyArabic, conflict.SourceKind);
                Assert.AreEqual(1m, conflict.DifferenceYuan.Value);
                Assert.AreEqual("D7", conflict.ExcelAmount.CellAddress);
            }
        }

        [TestMethod]
        public void Check_FormulaCacheMissing_Returns36IndeterminateWithoutSubstitutingZero()
        {
            using (var files = new SyntheticInputs(missingCache: true))
            {
                V3MoneyComparisonResult result = Check(files);

                Assert.AreEqual(36, result.TotalCount);
                Assert.AreEqual(36, result.IndeterminateCount);
                Assert.AreEqual(0, result.ConsistentCount);
                Assert.AreEqual(0, result.InconsistentCount);
                Assert.IsFalse(result.IsComplete);
                Assert.AreEqual(V3MoneyComparisonStatus.Indeterminate, result.OverallStatus);
                Assert.IsTrue(result.Items.All(item => !item.ExcelComparisonAmountYuan.HasValue
                    && !item.DifferenceYuan.HasValue && !string.IsNullOrWhiteSpace(item.Reason)));
            }
        }

        [TestMethod]
        public void Compare_ReportExtractionFailed_LeavesIts24AmountsIndeterminate()
        {
            using (var files = new SyntheticInputs())
            {
                WordConclusionExtractionResult report = WordConclusionExtractionResult.Failed(
                    WordConclusionExtractionStatus.IncompleteAmounts, "虚构报告缺少必要金额。");
                V3MoneyComparisonResult result = _service.Compare(report, Extract(files.Explanation,
                    WordConclusionDocumentKind.Explanation), new ExcelSummaryReader().Read(files.WorkbookPath), files.WorkbookPath);

                Assert.AreEqual(36, result.TotalCount);
                Assert.AreEqual(24, result.IndeterminateCount);
                Assert.AreEqual(12, result.ConsistentCount);
                Assert.AreEqual(0, result.InconsistentCount);
                Assert.IsFalse(result.IsComplete);
                Assert.IsTrue(result.Items.Where(item => item.Target != WordConclusionTarget.ExplanationChapterFour)
                    .All(item => item.Status == V3MoneyComparisonStatus.Indeterminate && item.WordAmount == null
                        && !item.DifferenceYuan.HasValue));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Compare_MissingOrDuplicateWordSlot_MarksOnlyThatSlotIndeterminate(bool duplicate)
        {
            using (var files = new SyntheticInputs())
            {
                WordConclusionExtractionResult report = Extract(files.Report, WordConclusionDocumentKind.Report);
                WordConclusion original = report.Conclusions.Single(conclusion => conclusion.Target == WordConclusionTarget.ReportSummary);
                WordConclusionAmount affected = original.Amounts.Single(amount => amount.SourceKind == WordConclusionSourceKind.TableAssetRow
                    && amount.ValueCategory == ValueCategory.AppraisedNetValue && amount.TaxBasis == TaxBasis.TaxExcluded);
                var replacement = new WordConclusion(original.SourceDocument, original.Target, original.HeadingLocation,
                    original.TableIndex, duplicate ? original.Amounts.Concat(new[] { affected })
                        : original.Amounts.Where(amount => !ReferenceEquals(amount, affected)));
                report = WordConclusionExtractionResult.Succeeded(report.Conclusions.Select(conclusion =>
                    conclusion.Target == original.Target ? replacement : conclusion));

                V3MoneyComparisonResult result = _service.Compare(report, Extract(files.Explanation,
                    WordConclusionDocumentKind.Explanation), new ExcelSummaryReader().Read(files.WorkbookPath), files.WorkbookPath);

                Assert.AreEqual(36, result.TotalCount);
                Assert.AreEqual(35, result.ConsistentCount);
                Assert.AreEqual(0, result.InconsistentCount);
                Assert.AreEqual(1, result.IndeterminateCount);
                Assert.IsFalse(result.IsComplete);
                V3MoneyComparisonItem unknown = result.Items.Single(item => item.Status == V3MoneyComparisonStatus.Indeterminate);
                Assert.AreEqual(original.Target, unknown.Target);
                Assert.AreEqual(affected.SourceKind, unknown.SourceKind);
                Assert.AreEqual(affected.ValueCategory, unknown.ValueCategory);
                Assert.AreEqual(affected.TaxBasis, unknown.TaxBasis);
                Assert.IsFalse(unknown.DifferenceYuan.HasValue);
                Assert.IsFalse(string.IsNullOrWhiteSpace(unknown.Reason));
            }
        }

        private V3MoneyComparisonResult Check(SyntheticInputs files) =>
            _service.Check(files.Report.FilePath, files.Explanation.FilePath, files.WorkbookPath);

        private static WordConclusionExtractionResult Extract(SyntheticDocxFile file, WordConclusionDocumentKind kind)
        {
            WordConclusionExtractionResult result = new WordConclusionExtractor().Extract(
                new DocxDocumentReader().Read(file.FilePath), file.FilePath, kind);
            Assert.IsTrue(result.IsSuccess, result.Message);
            return result;
        }

        private static void AssertAllConsistent(V3MoneyComparisonResult result)
        {
            Assert.AreEqual(36, result.TotalCount);
            Assert.AreEqual(36, result.ConsistentCount);
            Assert.AreEqual(0, result.InconsistentCount);
            Assert.AreEqual(0, result.IndeterminateCount);
            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(V3MoneyComparisonStatus.Consistent, result.OverallStatus);
        }

        private static int FieldIndex(ValueCategory category, TaxBasis tax) =>
            (tax == TaxBasis.TaxExcluded ? 0 : 2) + (category == ValueCategory.AppraisedOriginalValue ? 0 : 1);

        private sealed class SyntheticInputs : IDisposable
        {
            public SyntheticInputs(bool distinctTotals = false, string firstAssetNet = "149.38",
                string firstBodyNet = "149.38", bool missingCache = false)
            {
                string[] totals = DisplayAmounts.Select(value => (decimal.Parse(value, CultureInfo.InvariantCulture)
                    + (distinctTotals ? 1m : 0m)).ToString("0.00", CultureInfo.InvariantCulture)).ToArray();
                string narrative = Narrative(totals[1], totals[3], distinctTotals);
                Report = CreateDocx(Heading("资产评估报告摘要"),
                    Paragraph(distinctTotals ? narrative : Narrative(firstBodyNet, totals[3], false)), Unit(), Table(totals, firstAssetNet),
                    Heading("资产评估报告正文"), Heading("评估结论", "H2"), Paragraph(narrative), Unit(), Table(totals));
                Explanation = CreateDocx(Heading("第四章 评估结论及分析"), Paragraph(narrative), Unit(), Table(totals));
                WorkbookPath = Path.Combine(Path.GetTempPath(), "AssetAppraisalChecker-V3-Synthetic-" + Guid.NewGuid().ToString("N") + ".xlsx");
                CreateWorkbook(WorkbookPath, distinctTotals, missingCache);
            }

            public SyntheticDocxFile Report { get; }
            public SyntheticDocxFile Explanation { get; }
            public string WorkbookPath { get; }

            public void Dispose()
            {
                Report.Dispose();
                Explanation.Dispose();
                if (File.Exists(WorkbookPath)) File.Delete(WorkbookPath);
            }
        }

        private static string Narrative(string excluded, string included, bool distinctTotals) =>
            "评估值（不含增值税）合计为人民币" + (distinctTotals ? "壹佰伍拾万叁仟捌佰元" : "壹佰肆拾玖万叁仟捌佰元")
                + "（RMB" + excluded + "万元），评估值（含增值税）合计为人民币"
                + (distinctTotals ? "壹佰陆拾贰万捌仟壹佰元" : "壹佰陆拾壹万捌仟壹佰元") + "（RMB" + included + "万元）。";

        private static SyntheticDocxFile CreateDocx(params OpenXmlElement[] elements) => SyntheticDocxFile.CreateWithStyles(new[]
        {
            new W.Style(new W.StyleName { Val = "heading 1" }) { StyleId = "H1", Type = W.StyleValues.Paragraph },
            new W.Style(new W.StyleName { Val = "heading 2" }) { StyleId = "H2", Type = W.StyleValues.Paragraph }
        }, elements);

        private static W.Paragraph Heading(string text, string style = "H1") => new W.Paragraph(
            new W.ParagraphProperties(new W.ParagraphStyleId { Val = style }), new W.Run(new W.Text(text)));
        private static W.Paragraph Paragraph(string text) => new W.Paragraph(new W.Run(new W.Text(text)));
        private static W.Paragraph Unit() => Paragraph("金额单位：人民币万元");
        private static W.TableCell Cell(string text) => new W.TableCell(Paragraph(text));

        private static W.Table Table(string[] totalAmounts, string assetNet = "149.38")
        {
            return new W.Table(new W.TableGrid(Enumerable.Range(0, 6).Select(_ => new W.GridColumn()).ToArray()),
                new W.TableRow(new[] { "序号", "资产名称" }.Concat(Headers).Select(Cell).ToArray()),
                new W.TableRow(new[] { "1", "虚构设备A", "199.17", assetNet, "215.74", "161.81" }.Select(Cell).ToArray()),
                new W.TableRow(new[] { "合计", string.Empty }.Concat(totalAmounts).Select(Cell).ToArray()));
        }

        private static void CreateWorkbook(string path, bool distinctTotals, bool missingCache)
        {
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbook = document.AddWorkbookPart();
                workbook.Workbook = new S.Workbook(new S.Sheets());
                WorksheetPart sheet = workbook.AddNewPart<WorksheetPart>();
                var header = new S.Row(TextCell("A5", "序号"), TextCell("B5", "资产名称")) { RowIndex = 5 };
                for (int index = 0; index < Headers.Length; index++)
                    header.Append(TextCell(((char)('C' + index)).ToString() + "5", Headers[index]));
                sheet.Worksheet = new S.Worksheet(new S.SheetData(
                    new S.Row(TextCell("A1", "资产评估汇总表")) { RowIndex = 1 },
                    new S.Row(TextCell("A3", "金额单位：万元")) { RowIndex = 3 }, header,
                    AmountRow(6, false, distinctTotals, missingCache), AmountRow(7, true, distinctTotals, missingCache)));
                workbook.Workbook.GetFirstChild<S.Sheets>().Append(new S.Sheet
                {
                    Name = "资产评估汇总表", SheetId = 1, Id = workbook.GetIdOfPart(sheet)
                });
                sheet.Worksheet.Save();
                workbook.Workbook.Save();
            }
        }

        private static S.Row AmountRow(uint number, bool total, bool distinctTotals, bool missingCache)
        {
            var row = new S.Row(TextCell("A" + number, total ? "合计" : "1"),
                TextCell("B" + number, total ? string.Empty : "虚构设备A")) { RowIndex = number };
            for (int index = 0; index < RawAmounts.Length; index++)
            {
                string column = ((char)('C' + index)).ToString();
                string raw = (RawAmounts[index] + (total && distinctTotals ? 1m : 0m)).ToString(CultureInfo.InvariantCulture);
                row.Append(new S.Cell
                {
                    CellReference = column + number, DataType = S.CellValues.Number,
                    CellFormula = new S.CellFormula(total && !distinctTotals ? "SUM(" + column + "6)" : raw),
                    CellValue = missingCache && number == 6 && index == 1 ? null : new S.CellValue(raw)
                });
            }
            return row;
        }

        private static S.Cell TextCell(string address, string text) => new S.Cell
        {
            CellReference = address, DataType = S.CellValues.InlineString,
            InlineString = new S.InlineString(new S.Text(text))
        };
    }
}
