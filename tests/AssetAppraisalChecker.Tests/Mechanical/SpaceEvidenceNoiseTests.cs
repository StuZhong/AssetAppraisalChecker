using System;
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
    public sealed class SpaceEvidenceNoiseTests
    {
        [TestMethod]
        [DataRow("  合成标题  ")]
        [DataRow("　　合成资料核对已完成。")]
        [DataRow(" 合成资料核对已完成。 ")]
        [DataRow("   ")]
        [DataRow(" 38.58 ")]
        [DataRow("Section  title")]
        [DataRow("重置成本 = 数量  × 单价")]
        [DataRow("一、  合成说明。")]
        public void EdgesTitlesNumbersAndFormulas_WithoutErrorEvidenceAreNotIssues(string text)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P(text),
                new Table(new TableRow(new TableCell(P(text))))))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void GenuineInternalSpaces_AreFoundInBodyAndProseCell_WithOriginalRanges()
        {
            const string text = "合成资料  已经完成核对。";
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P(text),
                new Table(new TableRow(new TableCell(P(text))))))
            {
                var issues = Check(file);
                Assert.AreEqual(2, issues.Count);
                Assert.IsTrue(issues.All(item => item.Kind == MechanicalIssueKind.ConsecutiveSpaces));
                Assert.IsTrue(issues.All(item => item.Occurrences.Single().Evidence.Contains("字符偏移=4；长度=2")));
                Assert.IsTrue(issues[1].Occurrences.Single().Location.TableIndex.HasValue);
            }
        }

        [TestMethod]
        public void ExactOverlapIsReportedOnce_DifferentInternalRangeIsNotMerged()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                P("本次依据合成资料，"), P("  现场核查  已经完成。")))
            {
                var issues = Check(file);
                Assert.AreEqual(2, issues.Count);
                Assert.AreEqual(1, issues.Count(item => item.Kind == MechanicalIssueKind.LeadingSpace));
                Assert.AreEqual(1, issues.Count(item => item.Kind == MechanicalIssueKind.ConsecutiveSpaces));
                Assert.IsTrue(issues.Single(item => item.Kind == MechanicalIssueKind.LeadingSpace)
                    .Occurrences.Single().Evidence.Contains("字符偏移=0；长度=2"));
                Assert.IsTrue(issues.Single(item => item.Kind == MechanicalIssueKind.ConsecutiveSpaces)
                    .Occurrences.Single().Evidence.Contains("字符偏移=6；长度=2"));
            }
        }

        [TestMethod]
        public void TrailingOverlap_IsReportedOnce_OnlyWithContinuationEvidence()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                P("本次依据合成资料，  "), P("现场核查已经完成。")))
            {
                MechanicalIssue issue = Check(file).Single();
                Assert.AreEqual(MechanicalIssueKind.TrailingSpace, issue.Kind);
                Assert.IsTrue(issue.Occurrences.Single().Evidence.Contains("字符偏移=9；长度=2"));
            }
        }

        [TestMethod]
        public void MultipleInternalRanges_AreSeparate_AndRunSplittingDoesNotChangeDecision()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P("合成资料  已经完成  核查。"),
                new Paragraph(new Run(new Text("合成资料 ") { Space = SpaceProcessingModeValues.Preserve }),
                    new Run(new Text(" 已经完成  核查。") { Space = SpaceProcessingModeValues.Preserve }))))
            {
                var issues = Check(file);
                Assert.AreEqual(4, issues.Count);
                Assert.AreEqual(2, issues.Count(item => item.Occurrences.Single().Evidence.Contains("字符偏移=4；长度=2")));
                Assert.AreEqual(2, issues.Count(item => item.Occurrences.Single().Evidence.Contains("字符偏移=10；长度=2")));
            }
        }

        [TestMethod]
        public void MultiParagraphCell_UsesCellOffsetsWithoutCrossParagraphSpaceMatches()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(new TableRow(new TableCell(P("合成说明。"), P("合成资料  已经完成核对。"))))))
            {
                MechanicalIssue issue = Check(file).Single();
                Assert.IsTrue(issue.Occurrences.Single().Evidence.Contains("字符偏移=10；长度=2"));
                Assert.IsTrue(issue.Occurrences.Single().Evidence.Any(item => item.Contains("段内相对起点=4")));
                Assert.IsTrue(issue.Occurrences.Single().Evidence.Any(item => item.Contains("p[2]")));
                var read = new AssetAppraisalChecker.Document.DocxDocumentReader().Read(file.FilePath);
                var source = new CheckedDocumentSource("文档 A", file.FilePath,
                    CheckedDocumentSource.ComputeSha256(file.FilePath), read);
                var mapped = new MechanicalIssueMapper().Map(new[] { issue }, 0).Single().Occurrences.Single();
                SourceNavigationTarget target = new SourceLocationResolver().Resolve(source, mapped);
                Assert.IsNotNull(target);
                Assert.AreEqual(10, target.Offset);
                Assert.AreEqual(2, target.Length);
                Assert.AreEqual("  ", target.SpanText);
            }
        }

        [TestMethod]
        public void HeadingsFieldsMathAndTabs_AreNotPlainProseSpaceEvidence()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new ParagraphProperties(new OutlineLevel { Val = 0 }), new Run(new Text("合成资料  已经完成。"))),
                new Paragraph(new SimpleField(new Run(new Text("合成资料  已经完成。"))) { Instruction = "TOC" }),
                new Paragraph(new Run(new Text("合成资料  已经完成。")), new DocumentFormat.OpenXml.Math.OfficeMath()),
                new Paragraph(new Run(new Text("合成资料  已经完成。"), new TabChar()))))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void HeadingOrTableBetweenProse_CannotSupplyEdgeContinuationEvidence()
        {
            foreach (OpenXmlElement barrier in new OpenXmlElement[] {
                new Paragraph(new ParagraphProperties(new OutlineLevel { Val = 0 }), new Run(new Text("合成章节"))),
                new Table(new TableRow(new TableCell(P("合成单元格")))) })
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P("本次依据合成资料，"), barrier, P("  核查工作已经完成。")))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void DifferentCells_CannotSupplyEdgeContinuationEvidence()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(new Table(new TableRow(
                new TableCell(P("本次依据合成资料，")), new TableCell(P("  核查工作已经完成。"))))))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void ProductionSingleDual_KeepsSourcesPreciseEvidenceAndHashes()
        {
            using (SyntheticDocxFile a = SyntheticDocxFile.Create(P("合成资料  已经完成核对。")))
            using (SyntheticDocxFile b = SyntheticDocxFile.Create(P("合成资料  已经完成核对。")))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var service = new ReportCheckService();
                ReportCheckResult single = service.CheckAsync(new ReportCheckRequest(a.FilePath, null, null)).GetAwaiter().GetResult();
                ReportCheckResult dual = service.CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null)).GetAwaiter().GetResult();
                Assert.IsTrue(single.IsSuccess);
                Assert.IsTrue(dual.IsSuccess);
                Assert.AreEqual(1, single.Issues.Count(item => item.GovernanceKind == "ConsecutiveSpaces"));
                var spaces = dual.Issues.Where(item => item.GovernanceKind == "ConsecutiveSpaces").ToList();
                Assert.AreEqual(2, spaces.Count);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" }, spaces.Select(item => item.Occurrences.Single().DocumentLabel).ToArray());
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        private static IReadOnlyList<MechanicalIssue> Check(SyntheticDocxFile file)
        {
            var read = new AssetAppraisalChecker.Document.DocxDocumentReader().Read(file.FilePath);
            Assert.IsTrue(read.IsSuccess);
            return new WhitespaceAndCharacterChecker().Check(new MechanicalCheckContext(file.FilePath, read, "文档 A"))
                .Where(item => item.Kind == MechanicalIssueKind.LeadingSpace || item.Kind == MechanicalIssueKind.TrailingSpace ||
                    item.Kind == MechanicalIssueKind.ConsecutiveSpaces).ToList();
        }
        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
