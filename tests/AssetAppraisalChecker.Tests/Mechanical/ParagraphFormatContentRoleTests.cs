using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Desktop.Services.Navigation;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class ParagraphFormatContentRoleTests
    {
        [TestMethod]
        [DataRow(600, "=16,060.2元")]
        [DataRow(1200, "=2,157,420.63元")]
        [DataRow(1700, "=1,991,727.00元（取整）")]
        public void ResultLine_DoesNotBorrowFormulaContinuationOrLabelPeers(int indent, string text)
        {
            using (var file = Create(P("合成费用=100元×2", 200), P("（一）合成费用项目1", 200),
                P("合成费用=200元×3", 200), P("=300元×4", 200), P("合成费用=400元×5", 200),
                P("=500元×6", 200), P(text, indent)))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        [DataRow("=1,234.00元", "=2,345.00元")]
        [DataRow("合成费用=100元×2", "合成费用=200元×3")]
        [DataRow("=100元×2", "=200元×3")]
        [DataRow("×100元+2元", "×200元+3元")]
        [DataRow("本次已核实合成资料，相关内容均已确认。", "本次已核实合成内容，相关资料均已确认。")]
        public void ReliableSameRolePeers_StillDetectIsolatedIndentOutlier(string stable, string changed)
        {
            var elements = Enumerable.Range(0, 5).Select(i => P(i == 2 ? changed : stable, i == 2 ? 600 : 200)).ToArray();
            using (var file = Create(elements))
            {
                var issue = Check(file).Single();
                Assert.AreEqual(2, issue.Occurrences[0].Location.ParagraphIndex);
                Assert.IsTrue(issue.Occurrences[0].Evidence.Any(e => e.Contains("首行缩进")));
                Assert.IsTrue(issue.Occurrences.Skip(1).All(o => o.RawText == stable));
            }
        }

        [TestMethod]
        public void ShortNumberedLabel_CannotBorrowBodyPeers()
        {
            var paragraphs = Enumerable.Range(0, 5).Select(i => P("这是合成项目1的普通正文，已经完成核查。", 200)).ToList();
            paragraphs.Insert(2, P("1. 合成项目1费用", 600));
            using (var file = Create(paragraphs.ToArray())) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void FormulaAndResultRoles_CannotFillEachOthersMinimumPeerCount()
        {
            using (var file = Create(P("=100元", 600), P("=200元", 200), P("=300元", 200),
                P("合成金额=100元×2", 200), P("合成金额=200元×3", 200), P("=300元×4", 200)))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void RunSplitting_DoesNotChangeResultClassification()
        {
            var result = P("", 600);
            result.RemoveAllChildren<Run>();
            result.Append(new Run(new Text("=")), new Run(new Text("1,234.00")), new Run(new Text("元（取整）")));
            using (var file = Create(P("合成金额=100元×2", 200), P("合成金额=200元×3", 200),
                result, P("合成金额=300元×4", 200), P("合成金额=400元×5", 200)))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\u200B")]
        public void EmptyCellStyleSpacingDifference_HasNoVisibleAnomalyEvidence(string text)
        {
            using (var file = Create(SpacingTable(text, false)))
            {
                var read = new AssetAppraisalChecker.Document.DocxDocumentReader().Read(file.FilePath);
                var context = new MechanicalCheckContext(file.FilePath, read, "文档 B");
                var target = context.FormattingSnapshot.Paragraphs.Single(p => p.Location.RowIndex == 3 && p.Location.CellIndex == 1);
                Assert.AreEqual(1.25m, target.EffectiveLineSpacing.Value);
                Assert.AreEqual(AssetAppraisalChecker.Mechanical.Formatting.FormattingSource.ParagraphStyle, target.EffectiveLineSpacing.Source);
                Assert.AreEqual(0, new ParagraphFormatChecker().Check(context).Count);
            }
        }

        [TestMethod]
        public void HiddenOnlyCellText_DoesNotProveVisibleLayoutError()
        {
            var table = SpacingTable("隐藏测试内容", false);
            foreach (Run run in table.Descendants<Run>()) run.RunProperties = new RunProperties(new Vanish());
            using (var file = Create(table)) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void VisibleSameColumnSpacingOutlier_StillDetectedDespiteDifferentProvenance()
        {
            using (var file = Create(SpacingTable("合成测试内容", false)))
            {
                var issue = Check(file).Single();
                Assert.AreEqual(3, issue.Occurrences[0].Location.RowIndex);
                Assert.AreEqual(1, issue.Occurrences[0].Location.CellIndex);
                Assert.IsTrue(issue.Occurrences[0].Evidence.Any(e => e.Contains("1.25 LineMultiple（来源：ParagraphStyle）")));
                Assert.IsTrue(issue.Occurrences[0].Evidence.Any(e => e.Contains("1 LineMultiple（来源：DirectFormatting）")));
            }
        }

        [TestMethod]
        public void SameEffectiveSpacing_DifferentProvenanceDoesNotReport()
        {
            using (var file = Create(SpacingTable("合成测试内容", true))) Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void ProductionDualDocument_RetainsActualOutlierSourceLocationAndHashes()
        {
            using (var a = Create(P("合成金额=100元×2", 200), P("合成金额=200元×3", 200),
                P("=1,234.00元", 600), P("合成金额=300元×4", 200), P("合成金额=400元×5", 200)))
            using (var b = Create(SpacingTable("合成测试内容", false)))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var result = new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null)).GetAwaiter().GetResult();
                Assert.IsTrue(result.IsSuccess);
                var issue = result.Issues.Single(i => i.GovernanceKind == "ParagraphFormatOutlier");
                Assert.IsTrue(issue.Occurrences.All(o => o.DocumentLabel == "文档 B"));
                var source = SourceNavigationService.FindSource(result.SourceDocuments, issue.Occurrences[0]);
                Assert.IsNotNull(new SourceLocationResolver().Resolve(source, issue.Occurrences[0]));
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        private static Table SpacingTable(string text, bool equivalent)
        {
            var table = new Table(new TableRow(new TableRowProperties(new TableHeader()),
                new TableCell(P("项目", 200)), new TableCell(P("说明", 200))));
            for (int i = 0; i < 5; i++)
            {
                Paragraph paragraph = P(text, 200);
                if (i != 2) paragraph.ParagraphProperties.Append(new SpacingBetweenLines
                    { Line = equivalent ? "300" : "240", LineRule = LineSpacingRuleValues.Auto });
                table.Append(new TableRow(new TableCell(P("合成项目", 200)), new TableCell(paragraph)));
            }
            return table;
        }
        private static Paragraph P(string text, int indent) => new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }, new Indentation { FirstLineChars = indent }),
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        private static SyntheticDocxFile Create(params OpenXmlElement[] elements) => SyntheticDocxFile.CreateWithStyles(
            new[] { new Style(new StyleName { Val = "Normal" }, new StyleParagraphProperties(
                new Justification { Val = JustificationValues.Both }, new Indentation { FirstLineChars = 200 },
                new SpacingBetweenLines { Line = "300", LineRule = LineSpacingRuleValues.Auto }))
                { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true } }, elements);
        private static IReadOnlyList<MechanicalIssue> Check(SyntheticDocxFile file)
        {
            var read = new AssetAppraisalChecker.Document.DocxDocumentReader().Read(file.FilePath);
            Assert.IsTrue(read.IsSuccess);
            return new ParagraphFormatChecker().Check(new MechanicalCheckContext(file.FilePath, read, "文档 B"));
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path)) return System.BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
