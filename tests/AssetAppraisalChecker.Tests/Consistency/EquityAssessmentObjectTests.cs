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
    public sealed class EquityAssessmentObjectTests
    {
        private const string Entire = "评估对象为合成标的公司股东全部权益价值。";

        [TestMethod]
        [DataRow(Entire)]
        [DataRow("评估对象为合成标的公司的30%股权价值。")]
        public void ExplicitEquity_DoesNotRequireInventedAssetFields(string text)
        {
            using (var file = Create(text))
            {
                var fact = Extract(file).Single();
                Assert.IsNull(fact.Owner);
                Assert.IsNull(fact.Quantity);
                Assert.IsNull(fact.EconomicAction);
                Assert.IsNull(fact.AssetType);
                Assert.IsTrue(fact.IsEquity);
                Assert.AreEqual("合成标的公司", fact.EquitySubject);
                Assert.AreEqual(text == Entire ? 100m : 30m, fact.EquityPercentage);
                Assert.AreEqual(text, fact.OriginalText);
                Assert.AreEqual(0, fact.SourceLocation.ParagraphIndex);
            }
        }

        [TestMethod]
        [DataRow("评估对象为合成标的公司的全部股东权益的价值。", Entire)]
        [DataRow("本次资产评估的评估对象为合成标的公司股东全部权益价值。", Entire)]
        [DataRow("评估对象为合成标的公司部分股权价值（持股比例为30%）。", "评估对象为合成标的公司的30.00％股权价值。")]
        public void ControlledEquivalentExpressions_PassWithoutSemantic(string first, string second)
        {
            using (var a = Create(first, "评估范围为合成标的公司全部资产及负债。"))
            using (var b = Create(second))
                Assert.AreEqual(ConsistencyStatus.PASS, ObjectResult(Analyze(a, b)).Status);
        }

        [TestMethod]
        [DataRow("被评估单位为合成标的公司。")]
        [DataRow("评估范围为合成标的公司股东全部权益价值。")]
        [DataRow("项目名称：合成标的公司股东全部权益价值评估项目。")]
        [DataRow("评估对象为合成标的公司部分股权价值。")]
        [DataRow("评估对象为股东全部权益价值。")]
        [DataRow("例如，评估对象为合成标的公司股东全部权益价值。")]
        [DataRow("评估对象不是合成标的公司股东全部权益价值。")]
        [DataRow("评估对象为非合成标的公司股东全部权益价值。")]
        [DataRow("评估对象为合成标的公司约30%股权价值。")]
        [DataRow("评估对象为合成标的公司30%至40%股权价值。")]
        [DataRow("评估对象为合成甲公司或者合成乙公司股东全部权益价值。")]
        public void UnreliableOrOtherRole_DoesNotBecomeObject(string text)
        {
            using (var file = Create(text)) Assert.AreEqual(0, Extract(file).Count);
        }

        [TestMethod]
        public void UnresolvedPercentageOrCompetingAlias_DoesNotPromoteRemainingAssertion()
        {
            using (var a = Create(Entire))
            using (var missing = Create(Entire, "评估对象为合成标的公司部分股权价值。", "持股比例为30%。"))
            using (var alias = Create("合成甲公司（简称“合成标的”）。",
                "合成乙公司（简称“合成标的”）。", "评估对象为合成标的股东全部权益价值。"))
            {
                Assert.AreEqual(0, Extract(missing).Count);
                Assert.AreEqual(0, Extract(alias).Count);
                Assert.AreEqual(ConsistencyStatus.WARNING, ObjectResult(Analyze(a, missing)).Status);
                Assert.AreEqual(ConsistencyStatus.WARNING, ObjectResult(Analyze(a, alias)).Status);
            }
        }

        [TestMethod]
        public void AmbiguousPossessiveBoundary_DoesNotChooseBetweenDefinedEntities()
        {
            using (var file = Create("合成甲公司（简称“合成标的”）。", "合成乙公司（简称“合成标”）。",
                "评估对象为合成标的股东全部权益价值。"))
                Assert.AreEqual(0, Extract(file).Count);
        }

        [TestMethod]
        [DataRow("评估对象为合成另一公司股东全部权益价值。")]
        [DataRow("评估对象为合成标的公司30%股权价值。")]
        public void ExplicitSubjectOrInterestConflict_FailsWithoutSemantic(string changed)
        {
            using (var a = Create(Entire))
            using (var b = Create(changed))
            {
                Assert.AreEqual(ConsistencyStatus.FAIL, ObjectResult(Analyze(a, b)).Status);
                Assert.AreEqual(ConsistencyStatus.FAIL, ObjectResult(Analyze(b, a)).Status);
            }
        }

        [TestMethod]
        public void ExplicitPartialPercentageConflict_Fails()
        {
            using (var a = Create("评估对象为合成标的公司30%股权价值。"))
            using (var b = Create("评估对象为合成标的公司40%股权价值。"))
                Assert.AreEqual(ConsistencyStatus.FAIL, ObjectResult(Analyze(a, b)).Status);
        }

        [TestMethod]
        public void RepeatedEquivalentAssertions_CoalesceIdentityWithoutLosingEvidence()
        {
            using (var a = Create(Entire, "评估对象为合成标的公司的全部股东权益价值。"))
            using (var b = Create(Entire))
            {
                var result = ObjectResult(Analyze(a, b));
                Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
                Assert.AreEqual(3, result.Evidence.Count);
            }
        }

        [TestMethod]
        public void MultipleObjectsOrAssetVsEquity_DoNotGuessPairing()
        {
            using (var a = Create(Entire))
            using (var multiple = Create(Entire, "评估对象为合成另一公司股东全部权益价值。"))
            using (var asset = Create("评估对象为合成标的公司转让的机器设备，涉及12台设备。"))
            {
                Assert.AreEqual(ConsistencyStatus.WARNING, ObjectResult(Analyze(a, multiple)).Status);
                Assert.AreEqual(ConsistencyStatus.WARNING, ObjectResult(Analyze(a, asset)).Status);
                var comparer = new AssessmentObjectSemanticAwareComparer(new ForbiddenSemanticAnalyzer());
                Assert.AreEqual(ConsistencyStatus.WARNING, comparer.Compare(Extract(a).Single(), Extract(asset).Single()).Status);
            }
        }

        [TestMethod]
        public async Task ExplicitAliasAndRunSplits_KeepCanonicalSubjectAndDefinitionLocation()
        {
            using (var a = Create("合成标的有限责任公司（简称“合成标的”）。",
                "评估对象为合成标的股东全部权益价值。"))
            using (var b = SyntheticDocxFile.Create(new Paragraph(new Run(new Text("评估对")),
                new Run(new Text("象为合成标的有限责任公司股东全")), new Run(new Text("部权益价值。")))))
            using (var changed = Create("评估对象为合成标的有限责任公司30%股权价值。"))
            {
                Assert.AreEqual(1, Extract(a).Count, "Explicit alias object must be extracted.");
                var fact = Extract(a).Single();
                Assert.AreEqual("合成标的有限责任公司", fact.EquitySubject);
                Assert.AreEqual(1, fact.EquitySubjectAliasEvidence.Count, "Alias definition must be retained.");
                Assert.AreEqual(0, fact.EquitySubjectAliasEvidence.Single().Location.ParagraphIndex);
                Assert.AreEqual(ConsistencyStatus.PASS, ObjectResult(Analyze(a, b)).Status);
                Assert.AreEqual(ConsistencyStatus.FAIL, ObjectResult(Analyze(a, changed)).Status);
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, changed.FilePath, null));
                Assert.IsTrue(result.IsSuccess, result.Message);
                Assert.AreEqual(1, result.Issues.Count(item => item.FieldName == "评估对象"),
                    string.Join(" | ", result.Issues.Select(item => item.FieldName + ":" + item.Reason)));
                var occurrence = result.Issues.Single(item => item.FieldName == "评估对象")
                    .Occurrences.Single(item => item.DocumentLabel == "文档 A");
                Assert.AreEqual(1, occurrence.Location.ParagraphIndex);
                Assert.IsTrue(occurrence.NormalizedValue.Contains("股东全部权益价值"));
                Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("明确简称定义")));
            }
        }

        [TestMethod]
        public async Task TableEquityConflict_PreservesCellNotNeighbouringEntity()
        {
            using (var a = SyntheticDocxFile.Create(new Table(new TableRow(
                new TableCell(new Paragraph(new Run(new Text("被评估单位为合成标的公司。")))),
                new TableCell(new Paragraph(new Run(new Text(Entire))))))))
            using (var b = Create("评估对象为合成标的公司30%股权价值。"))
            {
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                var occurrence = result.Issues.Single(item => item.FieldName == "评估对象")
                    .Occurrences.Single(item => item.DocumentLabel == "文档 A");
                Assert.AreEqual(0, occurrence.Location.TableIndex);
                Assert.AreEqual(0, occurrence.Location.RowIndex);
                Assert.AreEqual(1, occurrence.Location.CellIndex);
                Assert.AreEqual(Entire, occurrence.RawText);
            }
        }

        [TestMethod]
        public void EquityModel_RejectsInconsistentTypeAndPercentage()
        {
            using (var file = Create(Entire))
            {
                var fact = Extract(file).Single();
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AssessmentObjectFact(
                    fact.SourceDocument, fact.SourceLocation, fact.OriginalText, fact.Confidence,
                    fact.EquitySubject, AssessmentEquityInterest.EntireShareholdersEquity, 30m, null));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AssessmentObjectFact(
                    fact.SourceDocument, fact.SourceLocation, fact.OriginalText, fact.Confidence,
                    fact.EquitySubject, AssessmentEquityInterest.PartialShareholding, 100m, null));
            }
        }

        [TestMethod]
        public async Task ExplicitEquityConflict_ProductionChainKeepsBothLocationsAndInputs()
        {
            using (var a = Create("委托人为合成委托公司。", Entire))
            using (var b = Create("评估范围为合成标的公司全部资产及负债。",
                "评估对象为合成标的公司30%股权价值。"))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var result = await new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
                Assert.IsTrue(result.IsSuccess);
                var issue = result.Issues.Single(item => item.FieldName == "评估对象");
                Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" },
                    issue.Occurrences.Select(item => item.DocumentLabel).ToArray());
                Assert.IsTrue(issue.Occurrences.All(item => item.Location.ParagraphIndex == 1));
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        private static ConsistencyResult ObjectResult(V2AnalysisResult result)
            => result.Results.Single(item => item.FactType == FactType.AssessmentObject
                && item.Identity.ResultRole != AppraisedEntityFact.ResultRole);

        private static System.Collections.Generic.IReadOnlyList<AssessmentObjectFact> Extract(SyntheticDocxFile file)
            => new AssessmentObjectFactExtractor().Extract(new DocxDocumentReader().Read(file.FilePath), "DocumentA");

        private static V2AnalysisResult Analyze(SyntheticDocxFile a, SyntheticDocxFile b)
        {
            var result = new V2DocumentAnalysisPipeline(new ForbiddenSemanticAnalyzer()).Analyze(
                new V2DocumentAnalysisInput(new DocxDocumentReader().Read(a.FilePath), "DocumentA"),
                new V2DocumentAnalysisInput(new DocxDocumentReader().Read(b.FilePath), "DocumentB"));
            Assert.IsFalse(result.Executions.Any(item => item.ExecutionStatus == FactCheckExecutionStatus.Failed),
                "No equity comparison may fall through to Semantic or hide an execution failure.");
            return result;
        }

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
                => throw new AssertFailedException("Explicit equity facts must not use Semantic.");
        }
    }
}
