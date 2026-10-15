using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Mechanical;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Mechanical
{
    [TestClass]
    public sealed class TemplateResidueTests
    {
        private const string Instruction = "【编制说明：本段仅供编制人员参考，定稿前请删除本段。】";

        [TestMethod]
        [DataRow(Instruction)]
        [DataRow("【编制提示：定稿前删除此段。】")]
        public void ExplicitAuthoringInstruction_IsDetectedAndLocated(string text)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P(text)))
            {
                MechanicalIssue issue = Check(file).Single();
                Assert.AreEqual(MechanicalIssueKind.TemplateInstructionResidue, issue.Kind);
                Assert.AreEqual(text, issue.Occurrences.Single().RawText);
                Assert.AreEqual(0, issue.Occurrences.Single().Location.ParagraphIndex);
                Assert.IsTrue(issue.Occurrences.Single().Evidence.Any(item => item.Contains("XML 节点=/document[1]/body[1]/p[1]")));
            }
        }

        [TestMethod]
        [DataRow("项目名称：【待填写：项目名称】", "【待填写：项目名称】", 5)]
        [DataRow("评估基准日：{{评估基准日}}", "{{评估基准日}}", 6)]
        [DataRow("{{报告编号}}", "{{报告编号}}", 0)]
        public void ExplicitKnownFieldPlaceholder_PreservesExactSpan(string text, string marker, int offset)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P(text)))
            {
                MechanicalIssue issue = Check(file).Single();
                Assert.AreEqual(MechanicalIssueKind.UnreplacedFieldPlaceholder, issue.Kind);
                Assert.AreEqual(marker, issue.Occurrences.Single().RawText);
                Assert.IsTrue(issue.Occurrences.Single().Evidence.Any(item =>
                    item == "字符偏移=" + offset + "；长度=" + marker.Length));
            }
        }

        [TestMethod]
        [DataRow("XX公司设备型号为XX-12，单位为元/台（含税）。")]
        [DataRow("本报告编制说明：评估过程遵循相关准则。")]
        [DataRow("请根据实际情况填写相关资料。")]
        [DataRow("引用原文：‘【编制提示：定稿前删除此段。】’")]
        [DataRow("【编制说明：请勿删除本段。】")]
        [DataRow("【编制说明：无需定稿前删除本段。】")]
        [DataRow("【编制说明：应保留“定稿前删除本段”字样。】")]
        [DataRow("【编制说明：定稿前删除本段为错误操作。】")]
        [DataRow("字段示例为【待填写：项目名称】，不是本项目内容。")]
        [DataRow("“{{项目名称}}”")]
        [DataRow("{{未知业务字段}}")]
        [DataRow("报告编号：【待填写：项目名称】")]
        [DataRow("项目名称：XX公司资产评估项目")]
        public void NormalAmbiguousAndQuotedText_IsNotClassifiedAsTemplateResidue(string text)
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(P(text)))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void CitationContextAndQuoteStyle_ProtectLiteralTemplateExamples()
        {
            var quoteStyle = new Style(new StyleName { Val = "引用" })
            { Type = StyleValues.Paragraph, StyleId = "SyntheticQuote" };
            using (SyntheticDocxFile file = SyntheticDocxFile.CreateWithStyles(new[] { quoteStyle },
                P("以下是模板摘录："), new Paragraph(), P(Instruction),
                P("正常业务内容。"),
                new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "SyntheticQuote" }),
                    new Run(new Text("{{项目名称}}"))),
                P("“引用内容开始。"), P("中间引用段落。"), P(Instruction), P("引用内容结束。”")))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void SplitRunsAndRepeatedText_UseStructuralLocationNotFirstTextMatch()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                P("以下为示例："), P("{{项目名称}}"), P("实际字段如下。"),
                new Paragraph(new Run(new Text("{{项目")), new Run(new Text("名称}}")))))
            {
                MechanicalIssue issue = Check(file).Single();
                Assert.AreEqual(3, issue.Occurrences.Single().Location.ParagraphIndex);
                Assert.AreEqual("{{项目名称}}", issue.Occurrences.Single().RawText);
            }
        }

        [TestMethod]
        public void FieldsAndAmbiguousMultiParagraphCells_AreNotAssumedToBeResidues()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Paragraph(new SimpleField(new Run(new Text("{{项目名称}}"))) { Instruction = " MERGEFIELD ProjectName " }),
                new Table(new TableRow(new TableCell(P("示例内容如下："), P("{{项目名称}}"))))))
                Assert.AreEqual(0, Check(file).Count);
        }

        [TestMethod]
        public void ProductionSingleAndDualChecks_PreserveSourceGovernanceEvidenceAndFileHashes()
        {
            using (SyntheticDocxFile a = SyntheticDocxFile.Create(P(Instruction)))
            using (SyntheticDocxFile b = SyntheticDocxFile.Create(
                new Table(new TableRow(new TableCell(P("项目名称：【待填写：项目名称】"))))))
            {
                string beforeA = Hash(a.FilePath), beforeB = Hash(b.FilePath);
                var service = new ReportCheckService();
                ReportCheckResult single = service.CheckAsync(new ReportCheckRequest(a.FilePath, null, null))
                    .GetAwaiter().GetResult();
                ReportCheckResult dual = service.CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null))
                    .GetAwaiter().GetResult();
                Assert.IsTrue(single.IsSuccess);
                Assert.IsTrue(dual.IsSuccess);
                Assert.AreEqual(1, TemplateIssues(single).Count);
                var issues = TemplateIssues(dual);
                Assert.AreEqual(2, issues.Count);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" }, issues
                    .Select(issue => issue.Occurrences.Single().DocumentLabel).ToArray());
                Assert.IsTrue(issues.All(issue => issue.Category == IssueCategory.Mechanical));
                Assert.IsTrue(issues.All(issue => issue.IssueTypeDisplayOverride == "文档残留"));
                var placeholder = issues.Single(issue => issue.GovernanceKind == "UnreplacedFieldPlaceholder");
                Assert.AreEqual(0, placeholder.Occurrences.Single().Location.TableIndex);
                Assert.AreEqual(0, placeholder.Occurrences.Single().Location.RowIndex);
                Assert.AreEqual(0, placeholder.Occurrences.Single().Location.CellIndex);
                Assert.AreEqual(beforeA, Hash(a.FilePath));
                Assert.AreEqual(beforeB, Hash(b.FilePath));
            }
        }

        private static List<ReportCheckIssue> TemplateIssues(ReportCheckResult result) => result.Issues
            .Where(issue => issue.GovernanceKind == "TemplateInstructionResidue" ||
                issue.GovernanceKind == "UnreplacedFieldPlaceholder").ToList();

        private static Paragraph P(string text) => new Paragraph(new Run(new Text(text)));

        private static IReadOnlyList<MechanicalIssue> Check(SyntheticDocxFile file) =>
            new DocumentResidueChecker().Check(new MechanicalCheckContext(file.FilePath,
                new DocxDocumentReader().Read(file.FilePath), "文档 A"));

        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
