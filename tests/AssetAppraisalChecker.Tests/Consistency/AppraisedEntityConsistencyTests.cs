using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Application;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class AppraisedEntityConsistencyTests
    {
        private const string Entity = "本次资产评估的被评估单位为合成标的有限责任公司。";

        [TestMethod]
        public async Task SameEntity_DifferentClientsAndScope_DoNotConflict()
        {
            using (var a = Create("委托人为合成委托甲公司。", Entity,
                "评估对象为合成标的有限责任公司股东全部权益价值。"))
            using (var b = Create("委托人为合成委托乙公司。", Entity,
                "评估范围为合成标的有限责任公司全部资产及负债。", Entity))
            {
                Assert.AreEqual(ConsistencyStatus.PASS, Analyze(a, b).Results.Single(
                    item => item.Identity.ResultRole == AppraisedEntityFact.ResultRole).Status);
                var result = await Check(a, b);
                Assert.AreEqual(0, result.Issues.Count(item => item.FieldName == "被评估单位"));
            }
        }

        [TestMethod]
        public async Task ExplicitAlias_HasDefinitionProvenanceWithoutInventingAuthority()
        {
            const string definition = "合成标的有限责任公司（以下简称“合成标的”）。";
            using (var a = Create(Entity))
            using (var b = Create(definition, "被评估单位：合成标的。"))
            {
                AppraisedEntityFact fact = Extract(b).Single();
                Assert.AreEqual("合成标的有限责任公司", fact.CompanyName);
                Assert.AreEqual(1, fact.SourceLocation.ParagraphIndex);
                Assert.AreEqual(0, fact.AliasEvidence.Single().Location.ParagraphIndex);
                Assert.AreEqual("被评估单位：合成标的。", fact.OriginalText);
                Assert.AreEqual(ConsistencyStatus.PASS, Analyze(a, b).Results.Single().Status);
                Assert.AreEqual(0, (await Check(a, b)).Issues.Count(item => item.FieldName == "被评估单位"));
            }
        }

        [TestMethod]
        public async Task ExplicitMutation_IsIndependentFromOwnerAndHasBothLocationsAndUnchangedHashes()
        {
            const string objectText = "评估对象为合成产权公司转让的机器设备，涉及12台设备。";
            using (var a = Create(objectText, Entity))
            using (var b = Create(objectText, Entity.Replace("合成标的有限责任公司", "合成另一有限责任公司")))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var analysis = Analyze(a, b);
                var execution = analysis.Executions.Single(item => item.CheckerName == AssessmentObjectChecker.Name);
                Assert.AreEqual(2, execution.Results.Count);
                Assert.AreEqual(ConsistencyStatus.PASS, execution.Results.Single(item =>
                    item.Identity.ResultRole == ConsistencyResultIdentity.DefaultResultRole).Status);
                Assert.AreEqual(ConsistencyStatus.FAIL, execution.Results.Single(item =>
                    item.Identity.ResultRole == AppraisedEntityFact.ResultRole).Status);
                Assert.AreEqual(FactCheckExecutionStatus.Succeeded, execution.ExecutionStatus);
                var issue = (await Check(a, b)).Issues.Single(item => item.FieldName == "被评估单位");
                Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
                Assert.AreEqual(2, issue.Occurrences.Count);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" },
                    issue.Occurrences.Select(item => item.DocumentLabel).ToArray());
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.ParagraphIndex == 1
                    && item.RawText.StartsWith("本次资产评估的被评估单位为", StringComparison.Ordinal)));
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
                Assert.AreEqual(IssueSeverity.Serious, (await Check(b, a)).Issues.Single(
                    item => item.FieldName == "被评估单位").Severity);
            }
        }

        [TestMethod]
        [DataRow("委托人为合成标的有限责任公司。")]
        [DataRow("项目名称：合成标的有限责任公司股权价值评估。")]
        [DataRow("产权持有人为合成标的有限责任公司。")]
        [DataRow("评估范围为合成标的有限责任公司全部资产。")]
        [DataRow("股东为合成标的有限责任公司。")]
        [DataRow("被评估单位可能为合成标的有限责任公司。")]
        [DataRow("被评估单位不是合成标的有限责任公司。")]
        [DataRow("被评估单位为合成甲公司或合成乙公司。")]
        [DataRow("被评估单位为合成甲公司，合成乙公司。")]
        [DataRow("被评估单位为合成简称。")]
        public void NonAuthoritativeOrAmbiguousText_DoesNotBecomeAnEntityFact(string text)
        {
            using (var file = Create(text)) Assert.AreEqual(0, Extract(file).Count);
        }

        [TestMethod]
        public void CompetingAliasOrRoleAssertions_FailClosedWithoutSelectingAMajority()
        {
            using (var a = Create(Entity))
            using (var b = Create("合成甲公司（简称“合成简称”）。", "合成乙公司（简称“合成简称”）。",
                "被评估单位为合成简称。", Entity))
            using (var competing = Create(Entity, Entity,
                "被评估单位为合成另一有限责任公司。"))
            {
                Assert.AreEqual(0, Extract(b).Count, "Do not silently omit the unresolved assertion.");
                Assert.AreEqual(ConsistencyStatus.WARNING, Analyze(a, b).Results.Single().Status);
                Assert.AreEqual(ConsistencyStatus.WARNING, Analyze(a, competing).Results.Single().Status);
            }
        }

        [TestMethod]
        public void MissingRole_DoesNotGuessFromOtherFields()
        {
            using (var a = Create(Entity))
            using (var b = Create("委托人为合成另一公司。"))
            {
                Assert.AreEqual(ConsistencyStatus.WARNING, Analyze(a, b).Results.Single().Status);
                Assert.IsFalse(Analyze(b, b).Results.Any(item => item.Identity.ResultRole == AppraisedEntityFact.ResultRole));
            }
        }

        [TestMethod]
        public void RunSplitAndInlineDefinition_KeepRoleAndSource()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(new Run(new Text("本次资产评估的被评")),
                new Run(new Text("估单位为合成标的有限责任公司（简称“合成标的”）。"))),
                new Paragraph(new Run(new Text("被评估单位：合成标的。")))))
            {
                Assert.AreEqual(2, Extract(file).Count);
                Assert.IsTrue(Extract(file).All(item => item.CompanyName == "合成标的有限责任公司"));
                Assert.AreEqual(0, Extract(file)[1].AliasEvidence.Single().Location.ParagraphIndex);
            }
        }

        [TestMethod]
        public async Task TableRoleAssertion_KeepsCellLocationWithoutBindingAdjacentClient()
        {
            using (var a = SyntheticDocxFile.Create(new Table(new TableRow(
                new TableCell(new Paragraph(new Run(new Text("委托人为合成委托公司。")))),
                new TableCell(new Paragraph(new Run(new Text(Entity))))))))
            using (var b = Create("被评估单位为合成另一公司。"))
            {
                var issue = (await Check(a, b)).Issues.Single(item => item.FieldName == "被评估单位");
                var occurrence = issue.Occurrences.Single(item => item.DocumentLabel == "文档 A");
                Assert.AreEqual(0, occurrence.Location.TableIndex);
                Assert.AreEqual(0, occurrence.Location.RowIndex);
                Assert.AreEqual(1, occurrence.Location.CellIndex);
                Assert.AreEqual("合成标的有限责任公司", occurrence.NormalizedValue);
                Assert.AreEqual(Entity, occurrence.RawText);
            }
        }

        [TestMethod]
        public async Task AliasConflict_PresentationRetainsDefinitionAndRoleAssertionSeparately()
        {
            using (var a = Create("合成标的有限责任公司（简称“合成标的”）。", "被评估单位为合成标的。"))
            using (var b = Create("被评估单位为合成另一公司。"))
            {
                var issue = (await Check(a, b)).Issues.Single(item => item.FieldName == "被评估单位");
                var occurrence = issue.Occurrences.Single(item => item.DocumentLabel == "文档 A");
                Assert.AreEqual(1, occurrence.Location.ParagraphIndex);
                Assert.AreEqual("被评估单位为合成标的。", occurrence.RawText);
                Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("明确简称定义")
                    && item.Contains("合成标的有限责任公司")));
            }
        }

        private static System.Collections.Generic.IReadOnlyList<AppraisedEntityFact> Extract(SyntheticDocxFile file)
            => new AppraisedEntityFactExtractor().Extract(new DocxDocumentReader().Read(file.FilePath), "DocumentA");

        private static V2AnalysisResult Analyze(SyntheticDocxFile a, SyntheticDocxFile b)
            => new V2DocumentAnalysisPipeline(new ForbiddenSemanticAnalyzer()).Analyze(
                new V2DocumentAnalysisInput(new DocxDocumentReader().Read(a.FilePath), "DocumentA"),
                new V2DocumentAnalysisInput(new DocxDocumentReader().Read(b.FilePath), "DocumentB"));

        private static Task<ReportCheckResult> Check(SyntheticDocxFile a, SyntheticDocxFile b)
            => new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));

        private static SyntheticDocxFile Create(params string[] paragraphs)
            => SyntheticDocxFile.Create(paragraphs.Select(text => new Paragraph(new Run(new Text(text)))).ToArray());

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream));
        }

        private sealed class ForbiddenSemanticAnalyzer : ISemanticAnalyzer
        {
            public SemanticAnalysisResult Analyze(string textContent)
                => throw new AssertFailedException("Explicit entity identity must not use Semantic.");
        }
    }
}
