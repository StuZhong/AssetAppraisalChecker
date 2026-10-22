using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Services;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Tests.Desktop
{
    [TestClass]
    public sealed class V3DesktopIntegrationTests
    {
        [TestMethod]
        public async Task Service_V3NormalInput_Reports36MatchesWithoutLegacyIssuesOrFileChanges()
        {
            using (var files = new ConclusionFiles())
            {
                string[] paths = { files.ReportPath, files.ExplanationPath, files.ExcelPath };
                string[] hashes = paths.Select(CheckedDocumentSource.ComputeSha256).ToArray();

                ReportCheckResult result = await new ReportCheckService().CheckAsync(files.Request());

                Assert.IsTrue(result.IsV3Check);
                Assert.IsTrue(result.IsSuccess, result.Message);
                Assert.AreEqual(36, result.V3Comparison.ConsistentCount);
                Assert.AreEqual(0, result.V3Comparison.InconsistentCount);
                Assert.AreEqual(0, result.V3Comparison.IndeterminateCount);
                Assert.AreEqual(0, result.Issues.Count, "V3 不应执行样本中无关的 V1 基准日规则。");
                Assert.AreEqual(files.ExcelPath, result.ExcelFilePath);
                Assert.AreEqual(2, result.SourceDocuments.Count);
                Assert.IsTrue(result.SourceDocuments.All(source => source.IsUnchanged()));
                StringAssert.Contains(result.Message, "36");
                CollectionAssert.AreEqual(hashes, paths.Select(CheckedDocumentSource.ComputeSha256).ToArray());
            }
        }

        [TestMethod]
        public async Task Service_SavedReportRemainsOpenForWriting_RecheckKeepsOriginalSourceIdentity()
        {
            using (var files = new ConclusionFiles())
            {
                string originalHash = CheckedDocumentSource.ComputeSha256(files.ReportPath);
                var service = new ReportCheckService();
                ReportCheckResult first = await service.CheckAsync(files.Request());
                Assert.AreEqual(36, first.V3Comparison.ConsistentCount);

                // WPS retains a write-capable handle after saving. This handle deliberately
                // stays open throughout the recheck; the checker must only read a snapshot.
                using (var savedOpenReport = new FileStream(files.ReportPath, FileMode.Open, FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    ReportCheckResult rechecked = await service.CheckAsync(files.Request());

                    Assert.IsTrue(rechecked.IsSuccess, rechecked.Message);
                    Assert.AreEqual(36, rechecked.V3Comparison.ConsistentCount);
                    Assert.AreEqual(0, rechecked.V3Comparison.InconsistentCount);
                    Assert.AreEqual(0, rechecked.V3Comparison.IndeterminateCount);
                    CheckedDocumentSource source = rechecked.SourceDocuments.Single(item => item.Label == "文档 A");
                    Assert.AreEqual(files.ReportPath, source.FullPath);
                    Assert.AreEqual(originalHash, source.Sha256);
                    Assert.IsTrue(source.IsUnchanged());
                    Assert.IsTrue(rechecked.V3Comparison.Items.All(item => item.WordSourceDocument == files.ReportPath
                        || item.WordSourceDocument == files.ExplanationPath));
                    Assert.AreEqual(originalHash, CheckedDocumentSource.ComputeSha256(files.ReportPath));
                }
            }
        }

        [TestMethod]
        public async Task ViewModel_OneError_PreservesD6EvidenceAndNavigationThenRereadsSavedFiles()
        {
            using (var files = new ConclusionFiles())
            {
                files.SetReportAssetNetValue("149.83");
                var service = new RecordingService();
                var launcher = new RecordingLauncher();
                MainWindowViewModel viewModel = SelectV3(files, service, launcher);

                await viewModel.StartCheckAsync();

                ReportCheckResult result = service.LastResult;
                Assert.AreEqual(35, result.V3Comparison.ConsistentCount);
                Assert.AreEqual(1, result.V3Comparison.InconsistentCount);
                Assert.AreEqual(0, result.V3Comparison.IndeterminateCount);
                Assert.AreEqual(1, viewModel.SeriousCount);
                Assert.AreEqual(1, viewModel.Issues.Count);
                viewModel.SelectedIssue = viewModel.Issues.Single();
                string details = viewModel.SelectedIssueDetails.Replace(",", string.Empty);
                foreach (string expected in new[] { "149.83", "149.38", "资产评估汇总表", "D6", "4500" })
                    StringAssert.Contains(details, expected);

                ReportCheckOccurrence occurrence = result.Issues.Single().Occurrences.First(item => item.DocumentLabel == "文档 A");
                CheckedDocumentSource source = SourceNavigationService.FindSource(result.SourceDocuments, occurrence);
                SourceNavigationTarget target = new SourceLocationResolver().Resolve(source, occurrence);
                Assert.IsNotNull(target, "金额证据必须能还原到原 Word 单元格，不能只有文件路径。");
                Assert.AreEqual(0, target.Location.TableIndex);
                Assert.AreEqual(1, target.Location.RowIndex);
                Assert.AreEqual(3, target.Location.CellIndex);
                Assert.AreEqual("149.83", target.SpanText);
                Assert.IsTrue(target.Offset.HasValue);
                viewModel.OpenSourceCommand.Execute(null);
                Assert.AreEqual(files.ReportPath, launcher.LastPath);

                // Only the test's isolated synthetic document is edited. The old identity must then expire.
                string oldIdentity = source.Sha256;
                files.SetReportAssetNetValue("149.38");
                Assert.IsFalse(source.IsUnchanged());
                var adapter = new SourceNavigationTests.RecordingWordAdapter();
                SourceNavigationResult stale = await new SourceNavigationService(adapter).NavigateAsync(source, occurrence);
                Assert.AreEqual(SourceNavigationStatus.Unavailable, stale.Status);
                Assert.AreEqual(0, adapter.Calls);

                await viewModel.StartCheckAsync();

                Assert.AreEqual(2, service.InvocationCount);
                Assert.AreEqual(ReportCheckMode.WordExcel, service.LastRequest.Mode);
                Assert.AreEqual(36, service.LastResult.V3Comparison.ConsistentCount);
                Assert.AreEqual(0, service.LastResult.V3Comparison.InconsistentCount);
                Assert.AreEqual(0, viewModel.Issues.Count);
                Assert.AreEqual(0, viewModel.SeriousCount);
                Assert.IsNull(viewModel.SelectedIssue);
                Assert.IsNull(viewModel.SelectedNavigationOccurrence);
                Assert.AreEqual(string.Empty, viewModel.SelectedIssueDetails);
                Assert.AreNotEqual(oldIdentity, service.LastResult.SourceDocuments.Single(item => item.Label == "文档 A").Sha256);
                StringAssert.Contains(viewModel.ResultMessage, "36");
            }
        }

        [TestMethod]
        public async Task ViewModel_MissingExcelFormulaCache_IsVisibleReviewWithoutMoneyConflict()
        {
            using (var files = new ConclusionFiles())
            {
                files.RemoveFormulaCache();
                var service = new RecordingService();
                MainWindowViewModel viewModel = SelectV3(files, service, new RecordingLauncher());

                await viewModel.StartCheckAsync();

                Assert.AreEqual(0, service.LastResult.V3Comparison.ConsistentCount);
                Assert.AreEqual(0, service.LastResult.V3Comparison.InconsistentCount);
                Assert.AreEqual(36, service.LastResult.V3Comparison.IndeterminateCount);
                Assert.IsTrue(viewModel.Issues.Count > 0, "无法判定必须在默认问题列表可见。");
                Assert.IsTrue(service.LastResult.Issues.All(issue => issue.Severity == IssueSeverity.Reminder));
                Assert.IsTrue(service.LastResult.Issues.All(issue => issue.Authority == IssueAuthority.ReviewSuggested));
                Assert.IsFalse(service.LastResult.Issues.Any(issue => issue.IssueType == IssueType.MoneyConflict));
                Assert.AreEqual(0, viewModel.SeriousCount);
                StringAssert.Contains(viewModel.ResultMessage, "无法判定");
                viewModel.SelectedIssue = viewModel.Issues.First();
                StringAssert.Contains(viewModel.SelectedIssueDetails, "C6");
            }
        }

        [TestMethod]
        public async Task ViewModel_ExplicitV3Mode_RequiresThreeRolesAndClearsResultsWhenInputsOrModeChange()
        {
            using (var files = new ConclusionFiles())
            {
                var service = new RecordingService();
                var viewModel = new MainWindowViewModel(service, new RecordingLauncher());
                Assert.IsFalse(viewModel.IsV3Mode);
                viewModel.TrySelectDocumentA(files.ReportPath);
                Assert.IsTrue(viewModel.CanStartCheck, "保留 V1 的单 Word 启动方式。");
                viewModel.IsV3Mode = true;
                Assert.IsFalse(viewModel.CanStartCheck);
                viewModel.TrySelectDocumentB(files.ExplanationPath);
                Assert.IsFalse(viewModel.CanStartCheck);
                Assert.IsFalse(viewModel.TrySelectExcel("错误格式.xls"));
                Assert.IsFalse(viewModel.CanStartCheck);
                Assert.IsTrue(viewModel.TrySelectExcel(files.ExcelPath));
                Assert.IsTrue(viewModel.CanStartCheck);

                await viewModel.StartCheckAsync();

                Assert.AreEqual(ReportCheckMode.WordExcel, service.LastRequest.Mode);
                Assert.AreEqual(files.ExcelPath, service.LastRequest.ExcelPath);
                StringAssert.Contains(viewModel.ResultMessage, "36");
                viewModel.ClearDocumentB();
                Assert.IsFalse(viewModel.CanStartCheck, "V3 缺说明不能退化成 V1。");
                Assert.AreEqual(0, viewModel.Issues.Count);
                Assert.AreEqual("尚未执行校检。", viewModel.ResultMessage);
                viewModel.IsV3Mode = false;
                Assert.IsTrue(viewModel.CanStartCheck);
                Assert.AreEqual(0, viewModel.TotalCount);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Service_LegacySingleAndDualWord_StillStartWithoutExcel(bool dual)
        {
            using (SyntheticDocxFile a = SyntheticDocxFile.Create(Paragraph("虚构报告，评估基准日为2026年1月1日。")))
            using (SyntheticDocxFile b = SyntheticDocxFile.Create(Paragraph("虚构说明，评估基准日为2026年1月1日。")))
            {
                ReportCheckResult result = await new ReportCheckService().CheckAsync(
                    new ReportCheckRequest(a.FilePath, dual ? b.FilePath : null, null));

                Assert.IsTrue(result.IsSuccess, result.Message);
                Assert.IsFalse(result.IsV3Check);
                Assert.IsNull(result.V3Comparison);
                Assert.AreEqual(dual ? 2 : 1, result.SourceDocuments.Count);
                Assert.IsTrue(result.SourceDocuments.All(source => source.IsUnchanged()));
            }
        }

        private static MainWindowViewModel SelectV3(ConclusionFiles files, RecordingService service, RecordingLauncher launcher)
        {
            var viewModel = new MainWindowViewModel(service, launcher) { IsV3Mode = true };
            Assert.IsTrue(viewModel.TrySelectDocumentA(files.ReportPath));
            Assert.IsTrue(viewModel.TrySelectDocumentB(files.ExplanationPath));
            Assert.IsTrue(viewModel.TrySelectExcel(files.ExcelPath));
            return viewModel;
        }

        private sealed class RecordingService : IReportCheckService
        {
            private readonly ReportCheckService _inner = new ReportCheckService();
            public ReportCheckRequest LastRequest { get; private set; }
            public ReportCheckResult LastResult { get; private set; }
            public int InvocationCount { get; private set; }

            public Task<ReportCheckResult> CheckAsync(string filePath, IProgress<string> progress = null) =>
                CheckAsync(new ReportCheckRequest(filePath, null), progress);

            public async Task<ReportCheckResult> CheckAsync(ReportCheckRequest request, IProgress<string> progress = null)
            {
                LastRequest = request;
                InvocationCount++;
                LastResult = await _inner.CheckAsync(request, progress);
                return LastResult;
            }
        }

        private sealed class RecordingLauncher : IReportFileLauncher
        {
            public string LastPath { get; private set; }
            public ReportFileLaunchResult Open(string filePath)
            {
                LastPath = filePath;
                return new ReportFileLaunchResult(true, "模拟打开文件；未执行真实 WPS 交互。");
            }
        }

        private const string Narrative = "评估值（不含增值税）合计为人民币壹佰肆拾玖万叁仟捌佰元（RMB149.38万元），"
            + "评估值（含增值税）合计为人民币壹佰陆拾壹万捌仟壹佰元（RMB161.81万元）。";
        private static readonly string[] Headers =
        {
            "评估原值（不含税）", "评估净值（不含税）", "评估原值（含税）", "评估净值（含税）"
        };
        private static readonly decimal[] RawAmounts = { 199.1727m, 149.3795m, 215.742m, 161.8065m };
        private static readonly string[] DisplayAmounts = { "199.17", "149.38", "215.74", "161.81" };

        private sealed class ConclusionFiles : IDisposable
        {
            private readonly SyntheticDocxFile _report;
            private readonly SyntheticDocxFile _explanation;

            public ConclusionFiles()
            {
                _report = CreateWord(Paragraph("评估基准日为2026年1月1日。评估基准日为2026年2月1日。"),
                    Paragraph("资产评估报告摘要", "H1"), Paragraph(Narrative), Paragraph("金额单位：万元"), ResultTable(),
                    Paragraph("资产评估报告正文", "H1"), Paragraph("评估结论", "H2"), Paragraph(Narrative),
                    Paragraph("金额单位：万元"), ResultTable());
                _explanation = CreateWord(Paragraph("第三章 成本法评估技术说明", "H1"), Paragraph("案例重置成本999万元。"),
                    Paragraph("第四章 评估结论及分析", "H1"), Paragraph(Narrative), Paragraph("金额单位：万元"), ResultTable());
                ExcelPath = Path.Combine(Path.GetTempPath(), "AssetAppraisalChecker-V3Desktop-" + Guid.NewGuid().ToString("N") + ".xlsx");
                using (SpreadsheetDocument document = SpreadsheetDocument.Create(ExcelPath, SpreadsheetDocumentType.Workbook))
                {
                    WorkbookPart workbook = document.AddWorkbookPart();
                    WorksheetPart sheet = workbook.AddNewPart<WorksheetPart>();
                    var data = new S.SheetData(new S.Row(ExcelText("A1", "资产评估汇总表")) { RowIndex = 1 },
                        new S.Row(ExcelText("A3", "金额单位：万元")) { RowIndex = 3 });
                    var header = new S.Row(ExcelText("A5", "序号"), ExcelText("B5", "资产名称")) { RowIndex = 5 };
                    for (int index = 0; index < Headers.Length; index++)
                        header.Append(ExcelText(((char)('C' + index)) + "5", Headers[index]));
                    data.Append(header, AmountRow(6, false), AmountRow(7, true));
                    sheet.Worksheet = new S.Worksheet(data);
                    workbook.Workbook = new S.Workbook(new S.Sheets(new S.Sheet
                    {
                        Name = "资产评估汇总表", SheetId = 1U, Id = workbook.GetIdOfPart(sheet)
                    }));
                    sheet.Worksheet.Save();
                    workbook.Workbook.Save();
                }
            }

            public string ReportPath => _report.FilePath;
            public string ExplanationPath => _explanation.FilePath;
            public string ExcelPath { get; }
            public ReportCheckRequest Request() => new ReportCheckRequest(ReportPath, ExplanationPath, null,
                ReportCheckMode.WordExcel, ExcelPath);

            public void SetReportAssetNetValue(string value)
            {
                using (WordprocessingDocument document = WordprocessingDocument.Open(ReportPath, true))
                {
                    W.TableCell cell = document.MainDocumentPart.Document.Body.Elements<W.Table>().First()
                        .Elements<W.TableRow>().ElementAt(1).Elements<W.TableCell>().ElementAt(3);
                    cell.RemoveAllChildren<W.Paragraph>();
                    cell.Append(Paragraph(value));
                    document.MainDocumentPart.Document.Save();
                }
            }

            public void RemoveFormulaCache()
            {
                using (SpreadsheetDocument document = SpreadsheetDocument.Open(ExcelPath, true))
                {
                    WorksheetPart sheet = document.WorkbookPart.WorksheetParts.Single();
                    sheet.Worksheet.Descendants<S.Cell>().Single(cell => cell.CellReference.Value == "C6").CellValue = null;
                    sheet.Worksheet.Save();
                }
            }

            public void Dispose()
            {
                _report.Dispose();
                _explanation.Dispose();
                if (File.Exists(ExcelPath)) File.Delete(ExcelPath);
            }
        }

        private static SyntheticDocxFile CreateWord(params OpenXmlElement[] elements) => SyntheticDocxFile.CreateWithStyles(new[]
        {
            new W.Style(new W.StyleName { Val = "heading 1" }) { StyleId = "H1", Type = W.StyleValues.Paragraph },
            new W.Style(new W.StyleName { Val = "heading 2" }) { StyleId = "H2", Type = W.StyleValues.Paragraph }
        }, elements);

        private static W.Paragraph Paragraph(string text, string style = null)
        {
            var paragraph = new W.Paragraph();
            if (style != null) paragraph.ParagraphProperties = new W.ParagraphProperties(new W.ParagraphStyleId { Val = style });
            for (int index = 0; index < text.Length; index += 3)
                paragraph.Append(new W.Run(new W.Text(text.Substring(index, Math.Min(3, text.Length - index)))));
            return paragraph;
        }

        private static W.Table ResultTable()
        {
            var table = new W.Table(new W.TableGrid(Enumerable.Range(0, 7).Select(_ => new W.GridColumn()).ToArray()));
            table.Append(new W.TableRow(new[] { "序号", "资产名称" }.Concat(Headers).Concat(new[] { "备注" }).Select(text => Cell(text))),
                new W.TableRow(new[] { "1", "虚构设备" }.Concat(DisplayAmounts).Concat(new[] { string.Empty }).Select(text => Cell(text))));
            var total = new W.TableRow(Cell("合计", 2));
            foreach (string value in DisplayAmounts) total.Append(Cell(value));
            total.Append(Cell(string.Empty));
            table.Append(total);
            return table;
        }

        private static W.TableCell Cell(string text, int span = 1) => new W.TableCell(
            new W.TableCellProperties(new W.GridSpan { Val = span }), Paragraph(text));

        private static S.Cell ExcelText(string address, string text) => new S.Cell
        {
            CellReference = address, DataType = S.CellValues.InlineString, InlineString = new S.InlineString(new S.Text(text))
        };

        private static S.Row AmountRow(uint rowIndex, bool total)
        {
            var row = new S.Row(ExcelText("A" + rowIndex, total ? "合计" : "1"),
                ExcelText("B" + rowIndex, total ? string.Empty : "虚构设备")) { RowIndex = rowIndex };
            for (int index = 0; index < RawAmounts.Length; index++)
            {
                string column = ((char)('C' + index)).ToString();
                string raw = RawAmounts[index].ToString(CultureInfo.InvariantCulture);
                row.Append(new S.Cell
                {
                    CellReference = column + rowIndex, DataType = S.CellValues.Number,
                    CellFormula = new S.CellFormula(total ? "SUM(" + column + "6:" + column + "6)" : raw),
                    CellValue = new S.CellValue(raw)
                });
            }
            return row;
        }
    }
}
