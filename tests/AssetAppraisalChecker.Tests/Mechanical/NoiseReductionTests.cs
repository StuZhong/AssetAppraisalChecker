using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.ViewModels;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class NoiseReductionTests
    {
        [TestMethod]
        public void ConfirmedTocStyleTab_IsExcluded_ButBodyTabRetainsEvidenceAndSource()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "TOC1" }),
                    new Run(new Text("合成章节"), new TabChar(), new Text("1"))),
                new Paragraph(new Run(new Text("正文"), new TabChar(), new Text("异常间隔。")))))
            {
                string before = Hash(file.FilePath);
                IReadOnlyList<MechanicalIssue> issues = Check(file, "文档 B");
                MechanicalIssue tab = issues.Single(item => item.Kind == MechanicalIssueKind.Tab);

                Assert.AreEqual(1, tab.Occurrences[0].Location.ParagraphIndex);
                Assert.AreEqual("文档 B", tab.Occurrences[0].SourceDocument);
                Assert.IsTrue(tab.Occurrences[0].Evidence.Any(item => item.Contains("U+0009")));
                Assert.AreEqual(before, Hash(file.FilePath));
            }
        }

        [TestMethod]
        public void NamedTocStyleTab_IsExcluded_WithoutSuppressingTableBodyTab()
        {
            var tocStyle = new Style(new StyleName { Val = "目录 1" })
            {
                Type = StyleValues.Paragraph,
                StyleId = "SyntheticDirectoryStyle"
            };
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateWithStyles(
                new[] { tocStyle },
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "SyntheticDirectoryStyle" }),
                    new Run(new Text("合成目录项"), new TabChar(), new Text("2"))),
                new Table(new TableRow(new TableCell(
                    new Paragraph(new Run(new Text("单元格"), new TabChar(), new Text("数据"))))))))
            {
                IReadOnlyList<MechanicalIssue> issues = Check(file, "文档 A");
                MechanicalIssue tab = issues.Single(item => item.Kind == MechanicalIssueKind.Tab);

                Assert.IsTrue(tab.Occurrences[0].Location.TableIndex.HasValue);
                Assert.AreEqual("文档 A", tab.Occurrences[0].SourceDocument);
            }
        }

        [TestMethod]
        public void ExplicitTocFieldTab_IsExcluded_WithoutSuppressingFollowingBodyTab()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(
                    new Run(new FieldCode(" TOC \\o \"1-3\" ")),
                    new Run(new Text("合成目录项"), new TabChar(), new Text("3"))),
                new Paragraph(new Run(new Text("正文"), new TabChar(), new Text("制表符。")))))
            {
                IReadOnlyList<MechanicalIssue> issues = Check(file, "文档 A");

                Assert.AreEqual(1, issues.Count(item => item.Kind == MechanicalIssueKind.Tab));
                Assert.AreEqual(1, issues.Single(item => item.Kind == MechanicalIssueKind.Tab)
                    .Occurrences[0].Location.ParagraphIndex);
            }
        }

        [TestMethod]
        public void LayoutBlanksAndEdgeSpacesWithoutErrorEvidence_AreNotReported()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("合成封面"))),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(new Run(new Text("  合成章节  ")))))
            {
                IReadOnlyList<IssueViewModel> views = Views(Check(file, "文档 A"));
                // Blank cover/section spacing alone is not error evidence. The previous
                // assertion froze the old empty-text heuristic, not an approved layout norm.
                Assert.IsFalse(views.Any(item => item.GovernanceKind == "ConsecutiveBlankParagraphs"));
                // No approved cover/title spacing norm: the old edge-space assertions
                // froze low-evidence candidates, not known errors. Do not merely hide them.
                Assert.IsFalse(views.Any(item => item.GovernanceKind == "LeadingSpace" ||
                    item.GovernanceKind == "TrailingSpace" || item.GovernanceKind == "ConsecutiveSpaces"));
            }
        }

        [TestMethod]
        public void ProseSpacesRetainEvidence_ButEnglishTitlePaddingHasNoErrorAuthority()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("评估  价值。"))),
                new Paragraph(new Run(new Text("Section  title")))))
            {
                IReadOnlyList<IssueViewModel> spaces = Views(Check(file, "文档 A"))
                    .Where(item => item.GovernanceKind == "ConsecutiveSpaces").ToList();

                Assert.AreEqual(1, spaces.Count);
                // Governance remains Detailed; the retained prose candidate still carries
                // its exact source range. Title padding is no longer promoted to a candidate.
                Assert.IsTrue(IssueAttentionPolicy.IsAuxiliary(spaces[0]));
                Assert.IsTrue(spaces[0].Occurrences[0].SourceOccurrence.Evidence
                    .Any(item => item.Contains("字符偏移=")));
            }
        }

        [TestMethod]
        public void ProductionCheck_SingleAndDualDocuments_KeepTabIssuesSeparateAndInputsUnchanged()
        {
            using (SyntheticDocxFile documentA = CreateTocAndBodyTab())
            using (SyntheticDocxFile documentB = CreateTocAndBodyTab())
            {
                string beforeA = Hash(documentA.FilePath);
                string beforeB = Hash(documentB.FilePath);
                var service = new ReportCheckService();

                ReportCheckResult single = service.CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, null, null)).GetAwaiter().GetResult();
                ReportCheckResult dual = service.CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, documentB.FilePath, null))
                    .GetAwaiter().GetResult();

                Assert.IsTrue(single.IsSuccess);
                Assert.IsTrue(dual.IsSuccess);
                Assert.AreEqual(1, single.Issues.Count(item => item.GovernanceKind == "Tab"));
                var dualTabs = dual.Issues.Where(item => item.GovernanceKind == "Tab").ToList();
                Assert.AreEqual(2, dualTabs.Count);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" }, dualTabs
                    .Select(item => item.Occurrences.Single().DocumentLabel).ToArray());
                Assert.AreEqual(beforeA, Hash(documentA.FilePath));
                Assert.AreEqual(beforeB, Hash(documentB.FilePath));
            }
        }

        [TestMethod]
        [DataRow("一、")]
        [DataRow("2.")]
        [DataRow("第三章")]
        public void ConfirmedToc_NumberTitleSeparator_IsLayoutNotBodySpacing(string marker)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                TocEntry(marker + "  合成章节标题")))
            {
                Assert.AreEqual(0, Check(file, "文档 A")
                    .Count(item => item.Kind == MechanicalIssueKind.ConsecutiveSpaces));
            }
        }

        [TestMethod]
        public void ConfirmedToc_SeparatorSplitAcrossRuns_HasSameDecision()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "TOC1" }),
                    new Run(new Text("一、 ") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }),
                    new Run(new Text(" 合成章节标题") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }),
                    new Run(new TabChar(), new Text("12")))))
            {
                Assert.AreEqual(0, Check(file, "文档 A")
                    .Count(item => item.Kind == MechanicalIssueKind.ConsecutiveSpaces));
            }
        }

        [TestMethod]
        public void BodyAndTableProseSpacesRemainDetected_NotNumberTitlePadding()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text("一、合成正文  已经完成。"))),
                new Paragraph(new Run(new Text("一、  合成标题"), new TabChar(), new Text("12"))),
                new Table(new TableRow(new TableCell(
                    new Paragraph(new Run(new Text("一、合成单元格  已经完成。")))))),
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "TOC1" }),
                    new Run(new Text("一、  没有页码结构的内容")))))
            {
                var issues = Check(file, "文档 B")
                    .Where(item => item.Kind == MechanicalIssueKind.ConsecutiveSpaces).ToList();
                // Number/title padding and unconfirmed layout are not typo evidence;
                // genuine internal prose spaces still work in both body and table.
                Assert.AreEqual(2, issues.Count);
                Assert.IsTrue(issues.All(item => item.Occurrences.Single().SourceDocument == "文档 B"));
            }
        }

        [TestMethod]
        public void ConfirmedToc_InternalTitleSpacesWithoutApprovedNorm_AreNotErrors()
        {
            const string text = "一、  合成  章节标题";
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(TocEntry(text)))
            {
                Assert.IsFalse(Check(file, "文档 A").Any(item => item.Kind == MechanicalIssueKind.ConsecutiveSpaces));
            }
        }

        [TestMethod]
        public void ProductionCheck_TocSeparatorsExcluded_SingleDualBodyEvidenceAndHashesPreserved()
        {
            using (SyntheticDocxFile documentA = SyntheticDocxFile.Create(
                TocEntry("一、  合成章节标题"),
                new Paragraph(new Run(new Text("评估  价值。")))))
            using (SyntheticDocxFile documentB = SyntheticDocxFile.Create(
                TocEntry("二、  合成章节标题"),
                new Paragraph(new Run(new Text("评估  价值。")))))
            {
                string beforeA = Hash(documentA.FilePath);
                string beforeB = Hash(documentB.FilePath);
                var service = new ReportCheckService();
                ReportCheckResult single = service.CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, null, null)).GetAwaiter().GetResult();
                ReportCheckResult dual = service.CheckAsync(
                    new ReportCheckRequest(documentA.FilePath, documentB.FilePath, null))
                    .GetAwaiter().GetResult();

                Assert.IsTrue(single.IsSuccess);
                Assert.IsTrue(dual.IsSuccess);
                Assert.AreEqual(1, single.Issues.Count(item => item.GovernanceKind == "ConsecutiveSpaces"));
                var spaces = dual.Issues.Where(item => item.GovernanceKind == "ConsecutiveSpaces").ToList();
                Assert.AreEqual(2, spaces.Count);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" }, spaces
                    .Select(item => item.Occurrences.Single().DocumentLabel).ToArray());
                Assert.IsTrue(spaces.All(item => item.Occurrences.Single().Location.ParagraphIndex == 1));
                Assert.AreEqual(beforeA, Hash(documentA.FilePath));
                Assert.AreEqual(beforeB, Hash(documentB.FilePath));
            }
        }

        private static Paragraph TocEntry(string title)
        {
            return new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "TOC1" }),
                new Run(new Text(title) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve },
                    new TabChar(), new Text("12")));
        }

        private static SyntheticDocxFile CreateTocAndBodyTab()
        {
            return SyntheticDocxFile.Create(
                new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "TOC1" }),
                    new Run(new Text("合成目录项"), new TabChar(), new Text("1"))),
                new Paragraph(new Run(new Text("正文"), new TabChar(), new Text("异常制表符。"))));
        }

        private static IReadOnlyList<MechanicalIssue> Check(SyntheticDocxFile file, string label)
        {
            DocumentReadResult read = new AssetAppraisalChecker.Document.DocxDocumentReader()
                .Read(file.FilePath);
            Assert.IsTrue(read.IsSuccess);
            return new WhitespaceAndCharacterChecker().Check(
                new MechanicalCheckContext(file.FilePath, read, label));
        }

        private static IReadOnlyList<IssueViewModel> Views(IEnumerable<MechanicalIssue> source)
        {
            return new MechanicalIssueMapper().Map(source, 0)
                .Select(item => new IssueViewModel(item)).ToList();
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
