using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class PunctuationAndChineseSpacingEvidenceTests
    {
        [TestMethod]
        [DataRow("：   ")]
        [DataRow("：\t")]
        [DataRow("：\u3000")]
        [DataRow(": \u00a0")]
        public void IntroductionColon_WithFollowingContent_IsValidDespiteTrailingWhitespace(string ending)
        {
            using (var file = SyntheticDocxFile.Create(
                P("本次已完成合成资料核对，具体核对情况及结论如下" + ending), P("核对结果均符合合成测试条件。")))
                Assert.AreEqual(0, Check(file, new PunctuationChecker(), MechanicalIssueKind.MissingEndingPunctuation).Count);
        }

        [TestMethod]
        public void ColonAlone_DoesNotExemptProseWithoutIntroductionOrFollowingContent()
        {
            using (var file = SyntheticDocxFile.Create(
                P("本次已完成合成资料核对，全部检查工作已经完成："), P("附加合成内容。"),
                P("本次已完成合成资料核对，具体核对情况及结论如下：")))
                Assert.AreEqual(2, Check(file, new PunctuationChecker(), MechanicalIssueKind.MissingEndingPunctuation).Count);
        }

        [TestMethod]
        [DataRow("Formula")]
        [DataRow("Table")]
        [DataRow("List")]
        public void ColonCanIntroduceActualFormulaTableOrList_WithoutFixedCuePhrase(string type)
        {
            OpenXmlElement following = type == "Table"
                ? (OpenXmlElement)new Table(new TableRow(new TableCell(P("合成栏目"))))
                : P(type == "Formula" ? "合成结果=1+2" : "（一）合成清单条目");
            using (var file = SyntheticDocxFile.Create(
                P("本次根据合成条件完成核对，以下为各项核对资料的展开内容：   "), new Paragraph(), following))
                Assert.AreEqual(0, Check(file, new PunctuationChecker(), MechanicalIssueKind.MissingEndingPunctuation).Count);
        }

        [TestMethod]
        public void NumberedNominalTitles_WithExplanatoryParagraph_AreNotSentences()
        {
            var automatic = P("可能对评估结果产生影响的事项，相关情形特别说明");
            automatic.PrependChild(new ParagraphProperties(new NumberingProperties(
                new NumberingLevelReference { Val = 0 }, new NumberingId { Val = 1 })));
            using (var file = SyntheticDocxFile.Create(
                P("一、合成报告的使用范围，限制条件及其对评估结论的影响"), P("此处为对应的合成解释正文。"),
                automatic, P("此处为对应的合成说明正文。")))
                Assert.AreEqual(0, Check(file, new PunctuationChecker(), MechanicalIssueKind.MissingEndingPunctuation).Count);
        }

        [TestMethod]
        public void OnlyNumberOrNominalEnding_IsNotEnoughToExemptActualProse()
        {
            using (var file = SyntheticDocxFile.Create(
                P("一、本次已经完成合成资料核查，所有核查结果均已得到确认"),
                P("本次核对已经完成，现将相关合成事项作出说明"), P("后续合成解释。")))
                Assert.AreEqual(2, Check(file, new PunctuationChecker(), MechanicalIssueKind.MissingEndingPunctuation).Count);
        }

        [TestMethod]
        public void RemovingFinalStopFromCompleteProse_IsDetectedWithoutChangingSourceEvidence()
        {
            const string valid = "本次已完成合成资产核实工作，相关资料已经得到逐项确认。";
            string mutated = valid.TrimEnd('。') + "   ";
            using (var clean = SyntheticDocxFile.Create(P(valid)))
            using (var bad = SyntheticDocxFile.Create(P(mutated)))
            {
                Assert.AreEqual(0, Check(clean, new PunctuationChecker(), MechanicalIssueKind.MissingEndingPunctuation).Count);
                var issue = Check(bad, new PunctuationChecker(), MechanicalIssueKind.MissingEndingPunctuation).Single();
                Assert.AreEqual(mutated, issue.Occurrences.Single().RawText);
                Assert.AreEqual(0, issue.Occurrences.Single().Location.ParagraphIndex);
                Assert.AreEqual(MechanicalIssueSeverity.Normal, issue.Severity);
            }
        }

        [TestMethod]
        [DataRow("《合成管理规定》（合成机关公布 根据修订决定调整）；")]
        [DataRow("《合成管理规定》（合成机关发布 根据修订决定调整）；")]
        [DataRow("《合成管理规定》（国务院令 第000号）")]
        [DataRow("《合成管理规定》（财政部 税务总局 海关总署公告）")]
        [DataRow("《合成规定》（机关甲 机关乙公告），本次据此编制。")]
        [DataRow("甲单位 乙单位分别提供资料，本次已经完成核查。")]
        [DataRow("本次采用资产评 估口径。")]
        public void CitationComponentsAndUnprovenWordBoundaries_AreNotSpaceErrors(string text)
        {
            using (var file = SyntheticDocxFile.Create(P(text), new Table(new TableRow(new TableCell(P(text))))))
                Assert.AreEqual(0, Check(file, new WhitespaceAndCharacterChecker(), MechanicalIssueKind.ChineseTextSpacing).Count);
        }

        [TestMethod]
        [DataRow("本次采用“资产评估”口径，原稿写成“资产评 估”口径。")]
        [DataRow("本次引用《合成评估准则》，原稿写成《合成评 估准则》。")]
        [DataRow("项目名称：合成甲项目；项目名 称：合成甲项目。")]
        public void InjectedSingleSpace_InsideBoundedTermWithIntactCounterpart_IsDetected(string text)
        {
            using (var file = SyntheticDocxFile.Create(P(text)))
            {
                var issue = Check(file, new WhitespaceAndCharacterChecker(), MechanicalIssueKind.ChineseTextSpacing).Single();
                int start = text.IndexOf(' ') - 1;
                Assert.IsTrue(issue.Occurrences.Single().Evidence.Contains("字符偏移=" + start + "；长度=3"));
                Assert.AreEqual(text, issue.Occurrences.Single().RawText);
                Assert.AreEqual(MechanicalIssueSeverity.Reminder, issue.Severity);
            }
        }

        [TestMethod]
        public void CounterpartMustBeWholeAndLocal_NotSubstringOrOtherDocumentElement()
        {
            using (var file = SyntheticDocxFile.Create(
                P("本次采用“合成资产评估”口径，原稿写成“资产评 估”口径。"),
                P("本次采用“资产评估”口径。"), P("本次采用“资产评 估”口径。")))
                Assert.AreEqual(0, Check(file, new WhitespaceAndCharacterChecker(), MechanicalIssueKind.ChineseTextSpacing).Count);
        }

        [TestMethod]
        public void RunSplitsAndTableCell_PreserveSingleSpaceEvidenceAndSource()
        {
            const string text = "本次采用“资产评估”口径，原稿写成“资产评 估”口径。";
            int split = text.IndexOf(' ');
            var paragraph = new Paragraph(new Run(new Text(text.Substring(0, split))),
                new Run(new Text(text.Substring(split)) { Space = SpaceProcessingModeValues.Preserve }));
            using (var file = SyntheticDocxFile.Create(new Table(new TableRow(new TableCell(paragraph)))))
            {
                var issue = Check(file, new WhitespaceAndCharacterChecker(), MechanicalIssueKind.ChineseTextSpacing).Single();
                Assert.AreEqual(0, issue.Occurrences.Single().Location.TableIndex);
                Assert.AreEqual("文档 B", issue.Occurrences.Single().SourceDocument);
                Assert.IsTrue(issue.Occurrences.Single().Evidence.Contains("字符偏移=" + (split - 1) + "；长度=3"));
            }
        }

        [TestMethod]
        public void ProductionSingleDual_RetainsMutationsAndBusinessConflict_SourcesSeverityAndReadOnly()
        {
            using (var a = SyntheticDocxFile.Create(P("评估基准日：2026年6月30日。"),
                P("本次已完成合成资料核对，具体核对情况及结论如下：   "), P("合成核对结果。"),
                P("本次已完成合成资产核实工作，相关资料已经得到逐项确认"),
                P("本次采用“资产评估”口径，原稿写成“资产评 估”口径。")))
            using (var b = SyntheticDocxFile.Create(P("评估基准日：2026年5月31日。"),
                P("本次已完成合成资产核实工作，相关资料已经得到逐项确认"),
                P("本次采用“资产评估”口径，原稿写成“资产评 估”口径。")))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var service = new ReportCheckService();
                var single = service.CheckAsync(new ReportCheckRequest(a.FilePath, null, null)).GetAwaiter().GetResult();
                var dual = service.CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null)).GetAwaiter().GetResult();
                Assert.IsTrue(single.IsSuccess);
                Assert.IsTrue(dual.IsSuccess);
                foreach (string kind in new[] { "MissingEndingPunctuation", "ChineseTextSpacing" })
                {
                    Assert.AreEqual(1, single.Issues.Count(item => item.GovernanceKind == kind));
                    var issues = dual.Issues.Where(item => item.GovernanceKind == kind).ToList();
                    Assert.AreEqual(2, issues.Count);
                    CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" },
                        issues.Select(item => item.Occurrences.Single().DocumentLabel).ToArray());
                }
                Assert.IsTrue(dual.Issues.Any(item => item.Severity == AssetAppraisalChecker.Core.Consistency.IssueSeverity.Serious &&
                    item.FieldName.Contains("基准日")));
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        private static IReadOnlyList<MechanicalIssue> Check(SyntheticDocxFile file, IMechanicalDocumentChecker checker, MechanicalIssueKind kind)
        {
            var read = new AssetAppraisalChecker.Document.DocxDocumentReader().Read(file.FilePath);
            Assert.IsTrue(read.IsSuccess);
            return checker.Check(new MechanicalCheckContext(file.FilePath, read, "文档 B")).Where(item => item.Kind == kind).ToList();
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
