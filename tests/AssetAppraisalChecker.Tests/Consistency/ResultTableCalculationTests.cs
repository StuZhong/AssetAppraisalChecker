using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class ResultTableCalculationTests
    {
        [TestMethod]
        [DataRow("25.00", "20.00%", "Increment")]
        [DataRow("20.00", "25.00%", "Rate")]
        public async Task ExplicitSameRowCalculationError_IsDetectedInProduction(string increment, string rate, string kind)
        {
            using (var file = File("100.00", "120.00", increment, rate))
            {
                string before = Hash(file.FilePath);
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(file.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                var issue = result.Issues.SingleOrDefault(item => item.GovernanceKind == "TableCalculation." + kind);
                Assert.IsNotNull(issue, "Explicit header-defined arithmetic errors must not be silently missed.");
                Assert.AreEqual(IssueSeverity.Normal, issue.Severity);
                Assert.IsFalse(IssueAttentionPolicy.IsAuxiliary(new IssueViewModel(issue)));
                Assert.AreEqual(kind == "Rate" ? 4 : 3, issue.Occurrences.Count);
                foreach (var occurrence in issue.Occurrences)
                {
                    Assert.AreEqual(0, occurrence.Location.TableIndex);
                    Assert.AreEqual(1, occurrence.Location.RowIndex);
                    var source = SourceNavigationService.FindSource(result.SourceDocuments, occurrence);
                    var target = new SourceLocationResolver().Resolve(source, occurrence);
                    Assert.IsNotNull(target);
                    Assert.AreEqual(occurrence.RawText, target.SpanText);
                }
                Assert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        [DataRow("100.00", "120.00", "20.00", "20.00%")]
        [DataRow("100.00", "80.00", "-20.00", "-20.00%")]
        [DataRow("100.00", "120.00", "20.01", "20.00%")]
        [DataRow("3.00", "4.00", "1.00", "33.33%")]
        [DataRow("0.00", "20.00", "20.00", "999.99%")]
        [DataRow("1000000元", "120.00万元", "200000元", "20.00%")]
        public async Task ValidRoundingZeroBookAndMixedUnits_DoNotGenerateCalculationIssues(
            string book, string appraisal, string increment, string rate)
        {
            using (var file = File(book, appraisal, increment, rate))
                await AssertCalculationCount(file.FilePath, 0);
        }

        [TestMethod]
        public async Task MissingUnitsUnknownOrCompetingHeaders_AbstainRatherThanPairAdjacentNumbers()
        {
            using (var file = SyntheticDocxFile.Create(P("评估结果汇总表"),
                T(R("项目", "账面价值", "评估价值", "增减值", "增值率（%）"), R("合成资产", "100", "120", "99", "99%"))))
                await AssertCalculationCount(file.FilePath, 0);
            using (var file = SyntheticDocxFile.Create(P("金额单位：万元"),
                T(R("项目", "方法一评估价值", "方法二评估价值", "增减值", "增值率（%）"), R("合成资产", "100", "120", "99", "99%"))))
                await AssertCalculationCount(file.FilePath, 0);
            using (var file = SyntheticDocxFile.Create(P("金额单位：万元"),
                T(R("项目", "账面价值", "评估价值", "增减值", "增值率（%）", "评估价值"),
                    R("合成资产", "100", "120", "99", "99%", "150"))))
                await AssertCalculationCount(file.FilePath, 0);
            using (var file = SyntheticDocxFile.Create(P("金额单位：万元"),
                T(R("项目", "账面原值", "评估净值", "增减值", "增值率（%）"), R("合成资产", "100", "120", "99", "99%"))))
                await AssertCalculationCount(file.FilePath, 0);
        }

        [TestMethod]
        public async Task DifferentMethodsInSeparateRows_AreNeverComparedAcrossRows()
        {
            using (var file = SyntheticDocxFile.Create(P("金额单位：万元"),
                T(Header(), R("成本法测算", "100.00", "120.00", "20.00", "20.00%"),
                    R("收益法测算", "100.00", "150.00", "50.00", "50.00%"))))
                await AssertCalculationCount(file.FilePath, 0);
        }

        [TestMethod]
        public async Task ZeroBook_SkipsRateButStillDetectsWrongIncrement()
        {
            using (var file = File("0.00", "20.00", "25.00", "999.99%"))
            {
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(file.FilePath, null));
                Assert.AreEqual(1, result.Issues.Count(item => item.GovernanceKind == "TableCalculation.Increment"));
                Assert.AreEqual(0, result.Issues.Count(item => item.GovernanceKind == "TableCalculation.Rate"));
            }
        }

        [TestMethod]
        public async Task AmbiguousMergedHeaderAndMultiParagraphNumber_Abstain()
        {
            var merged = new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), P("评估价值"));
            using (var file = SyntheticDocxFile.Create(P("金额单位：万元"),
                T(new TableRow(new TableCell(P("项目")), new TableCell(P("账面价值")), merged,
                    new TableCell(P("增减值")), new TableCell(P("增值率（%）"))), R("合成资产", "100", "120", "99", "99%"))))
                await AssertCalculationCount(file.FilePath, 0);
            var row = R("合成资产", "100", "120", "99", "99%");
            row.Elements<TableCell>().ElementAt(2).Append(P("150"));
            using (var file = SyntheticDocxFile.Create(P("金额单位：万元"), T(Header(), row)))
                await AssertCalculationCount(file.FilePath, 0);
        }

        [TestMethod]
        public async Task DualDocumentAndReversal_PreserveSourceAndOriginalHashes()
        {
            using (var a = File("100.00", "120.00", "25.00", "20.00%"))
            using (var b = File("100.00", "120.00", "20.00", "25.00%"))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                foreach (var paths in new[] { new[] { a.FilePath, b.FilePath }, new[] { b.FilePath, a.FilePath } })
                {
                    var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(paths[0], paths[1], null));
                    Assert.IsTrue(result.IsSuccess);
                    var issues = result.Issues.Where(item => item.GovernanceKind.StartsWith("TableCalculation.", StringComparison.Ordinal)).ToArray();
                    Assert.AreEqual(2, issues.Length);
                    foreach (var issue in issues)
                    foreach (var occurrence in issue.Occurrences)
                    {
                        string expected = issue.GovernanceKind.EndsWith("Increment", StringComparison.Ordinal) ? a.FilePath : b.FilePath;
                        var source = SourceNavigationService.FindSource(result.SourceDocuments, occurrence);
                        Assert.AreEqual(expected, source.FullPath);
                        Assert.AreEqual(expected == paths[0] ? "文档 A" : "文档 B", occurrence.DocumentLabel);
                    }
                }
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        private static async Task AssertCalculationCount(string path, int count)
        {
            var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(path, null));
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(count, result.Issues.Count(item => item.GovernanceKind.StartsWith("TableCalculation.", StringComparison.Ordinal)));
        }

        private static SyntheticDocxFile File(string book, string appraisal, string increment, string rate) =>
            SyntheticDocxFile.Create(P("评估结果汇总表"), P("金额单位：万元"),
                T(Header(), R("合成资产", book, appraisal, increment, rate)));
        private static TableRow Header() => R("项目", "账面价值", "评估价值", "增减值", "增值率（%）");
        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text)));
        private static TableRow R(params string[] cells) => new TableRow(cells.Select(text => new TableCell(P(text))));
        private static Table T(params TableRow[] rows)
        {
            var table = new Table(new TableGrid(Enumerable.Range(0, rows.Max(row =>
                row.Elements<TableCell>().Sum(cell => (int?)cell.TableCellProperties?.GetFirstChild<GridSpan>()?.Val?.Value ?? 1)))
                .Select(index => new GridColumn { Width = "1500" })));
            table.Append(rows);
            return table;
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = System.IO.File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
