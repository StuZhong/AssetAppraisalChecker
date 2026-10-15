using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class ConsecutiveBlankParagraphTests
    {
        private const string Before = "本次依据已收集的合成资料，";
        private const string After = "对合成资产进行现场核查。";

        [TestMethod]
        public void InterruptedProse_WithUnexplainedAdjacentBlanks_RetainsBothLocations()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Prose(Before), new Paragraph(), new Paragraph(), Prose(After)))
            {
                MechanicalIssue issue = Check(file, "文档 B").Single();
                Assert.AreEqual(MechanicalIssueSeverity.Reminder, issue.Severity);
                Assert.AreEqual(2, issue.Occurrences.Count);
                CollectionAssert.AreEqual(new int?[] { 1, 2 }, issue.Occurrences
                    .Select(item => item.Location.ParagraphIndex).ToArray());
                Assert.IsTrue(issue.Occurrences.All(item => item.SourceDocument == "文档 B"));
                Assert.IsTrue(issue.Occurrences.All(item => item.Evidence.Any(value => value.Contains("原始正文结构相邻")) ||
                    item.Evidence.Any(value => value.Contains("结构性空白"))));
            }
        }

        [TestMethod]
        public void HeadingBetweenBlanks_CannotCreateFilteredAdjacency()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Prose(Before), new Paragraph(),
                new Paragraph(new ParagraphProperties(new OutlineLevel { Val = 0 }), new Run(new Text("合成标题"))),
                new Paragraph(), Prose(After)))
            {
                Assert.AreEqual(0, Check(file).Count);
            }
        }

        [TestMethod]
        public void TableAndOpaqueBlocks_AreAdjacencyBarriers()
        {
            foreach (OpenXmlElement barrier in new OpenXmlElement[]
            {
                new Table(new TableRow(new TableCell(Prose("合成单元格")))),
                new SdtBlock(new SdtContentBlock(Prose("合成内容控件")))
            })
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Prose(Before), new Paragraph(), barrier, new Paragraph(), Prose(After)))
            {
                Assert.AreEqual(0, Check(file).Count);
            }
        }

        [TestMethod]
        [DataRow("page")]
        [DataRow("column")]
        [DataRow("line")]
        [DataRow("nextPageSection")]
        [DataRow("continuousSection")]
        [DataRow("pageBreakBefore")]
        [DataRow("field")]
        [DataRow("drawing")]
        [DataRow("bookmark")]
        public void MeaningfulBlankStructure_IsNotEmptySpacing(string kind)
        {
            Paragraph meaningful;
            switch (kind)
            {
                case "page": meaningful = new Paragraph(new Run(new Break { Type = BreakValues.Page })); break;
                case "column": meaningful = new Paragraph(new Run(new Break { Type = BreakValues.Column })); break;
                case "line": meaningful = new Paragraph(new Run(new Break())); break;
                case "nextPageSection": meaningful = Section(SectionMarkValues.NextPage); break;
                case "continuousSection": meaningful = Section(SectionMarkValues.Continuous); break;
                case "pageBreakBefore": meaningful = new Paragraph(new ParagraphProperties(new PageBreakBefore())); break;
                case "field": meaningful = new Paragraph(new SimpleField { Instruction = "PAGE" }); break;
                case "drawing": meaningful = new Paragraph(new Run(new Drawing())); break;
                default: meaningful = new Paragraph(new BookmarkStart { Id = "1", Name = "SyntheticAnchor" },
                    new BookmarkEnd { Id = "1" }); break;
            }
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Prose(Before), new Paragraph(), meaningful, new Paragraph(), Prose(After)))
            {
                Assert.AreEqual(0, Check(file).Count, kind);
            }
        }

        [TestMethod]
        public void SectionOnFollowingProse_ProtectsWholeBlankRun()
        {
            Paragraph after = Prose(After);
            after.PrependChild(new ParagraphProperties(new SectionProperties(
                new SectionType { Val = SectionMarkValues.NextPage })));
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Prose(Before), new Paragraph(), new Paragraph(), after))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void InheritedPageBreak_AndUnresolvedStyle_FailClosed()
        {
            var style = new Style(new StyleParagraphProperties(new PageBreakBefore()))
                { StyleId = "SyntheticPage", Type = StyleValues.Paragraph };
            foreach (string styleId in new[] { "SyntheticPage", "MissingStyle" })
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateWithStyles(new[] { (Style)style.CloneNode(true) },
                Styled(Before, styleId), Styled(string.Empty, styleId),
                Styled(string.Empty, styleId), Styled(After, styleId)))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        [DataRow("合成封面", "合成章节")]
        [DataRow("此前工作已完成。", "下一阶段工作已经完成。")]
        public void LayoutOrCompletedParagraphs_EmptyTextAloneDoesNotProveAnomaly(string before, string after)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Prose(before), new Paragraph(), new Paragraph(), Prose(after)))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void LeadingTrailingAndSingleBlanks_AreNotReported()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(), new Paragraph(), Prose(Before), new Paragraph(), Prose(After),
                new Paragraph(), new Paragraph()))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void SplitRuns_DoNotChangeContinuationEvidence_AndLongRunsKeepPairGranularity()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new Run(new Text(Before.Substring(0, 4))), new Run(new Text(Before.Substring(4)))),
                new Paragraph(), new Paragraph(), new Paragraph(), new Paragraph(),
                new Paragraph(new Run(new Text(After.Substring(0, 3))), new Run(new Text(After.Substring(3))))))
                Assert.AreEqual(2, Check(file).Count);
        }

        [TestMethod]
        public void NamedTocStyle_DoesNotBecomeProse_EvenWithoutVisibleFieldOrTab()
        {
            var style = new Style(new StyleName { Val = "目录 1" })
                { StyleId = "SyntheticDirectory", Type = StyleValues.Paragraph };
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateWithStyles(new[] { style },
                Styled(Before, style.StyleId), Styled(string.Empty, style.StyleId),
                Styled(string.Empty, style.StyleId), Styled(After, style.StyleId)))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void StructuralTextDoesNotAgreeWithReadSnapshot_FailsClosed()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                Prose(Before), new Paragraph(), new Paragraph(), Prose(After)))
            {
                DocumentReadResult read = new AssetAppraisalChecker.Document.DocxDocumentReader().Read(file.FilePath);
                var changed = read.Elements.Select(item => new DocumentElement(item.ElementType,
                    item.Location.ParagraphIndex == 0 ? "另一段合成文字，" : item.Text,
                    item.Location, item.DocumentOrderIndex, item.StyleId, item.NearbyContext));
                var context = new MechanicalCheckContext(file.FilePath,
                    DocumentReadResult.Succeeded(changed, 4, 0), "文档 A");
                Assert.IsFalse(new WhitespaceAndCharacterChecker().Check(context)
                    .Any(item => item.Kind == MechanicalIssueKind.ConsecutiveBlankParagraphs));
            }
        }

        [TestMethod]
        public void OnlyTableCellBlanks_DoNotBecomeBodyCandidates()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(new TableRow(new TableCell(Prose(Before), new Paragraph(), new Paragraph(), Prose(After))))))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void MissingPhysicalStructure_DoesNotInventBlankEvidence()
        {
            var elements = new[] { Before, "", "", After }.Select((text, index) => new DocumentElement(
                DocumentElementType.Paragraph, text,
                new DocumentLocation(DocumentPart.MainBody, index, null, null, null, new string[0]),
                index, string.Empty, null));
            var context = new MechanicalCheckContext("nonexistent-synthetic.docx",
                DocumentReadResult.Succeeded(elements, 4, 0), "文档 A");
            Assert.IsFalse(new WhitespaceAndCharacterChecker().Check(context)
                .Any(item => item.Kind == MechanicalIssueKind.ConsecutiveBlankParagraphs));
        }

        [TestMethod]
        public void ProductionSingleDual_KeepsSourcesLocationsAndInputHashes()
        {
            using (SyntheticDocxFile a = SyntheticDocxFile.Create(Prose(Before), new Paragraph(), new Paragraph(), Prose(After)))
            using (SyntheticDocxFile b = SyntheticDocxFile.Create(Prose(Before), new Paragraph(), new Paragraph(), Prose(After)))
            {
                string beforeA = Hash(a.FilePath), beforeB = Hash(b.FilePath);
                var service = new ReportCheckService();
                ReportCheckResult single = service.CheckAsync(new ReportCheckRequest(a.FilePath, null, null))
                    .GetAwaiter().GetResult();
                ReportCheckResult dual = service.CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null))
                    .GetAwaiter().GetResult();
                Assert.IsTrue(single.IsSuccess);
                Assert.IsTrue(dual.IsSuccess);
                Assert.AreEqual(1, single.Issues.Count(item => item.GovernanceKind == "ConsecutiveBlankParagraphs"));
                var issues = dual.Issues.Where(item => item.GovernanceKind == "ConsecutiveBlankParagraphs").ToList();
                Assert.AreEqual(2, issues.Count);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" }, issues
                    .Select(item => item.Occurrences.First().DocumentLabel).ToArray());
                Assert.IsTrue(issues.All(item => item.Occurrences.Count == 2));
                Assert.AreEqual(beforeA, Hash(a.FilePath));
                Assert.AreEqual(beforeB, Hash(b.FilePath));
            }
        }

        private static Paragraph Prose(string text) => new Paragraph(new Run(new Text(text)));
        private static Paragraph Styled(string text, string style) => new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = style }), new Run(new Text(text)));
        private static Paragraph Section(SectionMarkValues kind) => new Paragraph(
            new ParagraphProperties(new SectionProperties(new SectionType { Val = kind })));

        private static IReadOnlyList<MechanicalIssue> Check(SyntheticDocxFile file, string label = "文档 A")
        {
            DocumentReadResult read = new AssetAppraisalChecker.Document.DocxDocumentReader().Read(file.FilePath);
            Assert.IsTrue(read.IsSuccess);
            return new WhitespaceAndCharacterChecker().Check(new MechanicalCheckContext(file.FilePath, read, label))
                .Where(item => item.Kind == MechanicalIssueKind.ConsecutiveBlankParagraphs).ToList();
        }

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
