using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AssetAppraisalChecker.Application;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Desktop.Workflow;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class FinalEvaluationMethodTests
    {
        [TestMethod]
        public async Task ParticipationAndCalculation_AreNotTheFinalMethodSet()
        {
            using (var a = Create("企业整体采用资产基础法和收益法，最终选用资产基础法。"))
            using (var b = Create("企业整体采用收益法测算，最终采用资产基础法。"))
            {
                var extraction = Read(a, "DocumentA");
                Assert.AreEqual(3, extraction.Facts.Count);
                Assert.AreEqual(1, extraction.Facts.Count(f => f.MethodRole == EvaluationMethodRole.FinalAdopted));
                Assert.AreEqual(EvaluationMethodCode.AssetBasedApproach,
                    extraction.Facts.Single(f => f.MethodRole == EvaluationMethodRole.FinalAdopted).MethodCode);
                Assert.AreEqual(ConsistencyStatus.PASS, Execute(a, b).Results.Single().Status);
                var result = await Check(a, b);
                Assert.IsTrue(result.IsSuccess);
                Assert.AreEqual(0, result.Issues.Count(issue => issue.FieldName == "评估方法"));
            }
        }

        [TestMethod]
        public async Task FinalConflict_IsDetectedEvenWhenParticipatingSetsAreIdentical()
        {
            using (var a = Create("本次评估采用资产基础法和收益法，最终选用资产基础法。"))
            using (var b = Create("本次评估采用资产基础法和收益法，最终选用收益法。"))
            {
                string hashA = Hash(a.FilePath), hashB = Hash(b.FilePath);
                var execution = Execute(a, b);
                Assert.AreEqual(FactCheckExecutionStatus.Succeeded, execution.ExecutionStatus);
                Assert.AreEqual(ConsistencyStatus.FAIL, execution.Results.Single().Status);
                Assert.AreEqual(EvaluationMethodComparer.FinalResultRole, execution.Results.Single().Identity.ResultRole);
                var issue = (await Check(a, b)).Issues.Single(item => item.FieldName == "评估方法");
                CollectionAssert.AreEquivalent(new[] { "文档 A", "文档 B" },
                    issue.Occurrences.Select(item => item.DocumentLabel).Distinct().ToArray());
                Assert.AreEqual(2, issue.Occurrences.Count);
                Assert.IsTrue(issue.Occurrences.All(item => item.RawText.Contains("最终") && !string.IsNullOrEmpty(item.LocationText)));
                Assert.AreEqual(hashA, Hash(a.FilePath));
                Assert.AreEqual(hashB, Hash(b.FilePath));
            }
        }

        [TestMethod]
        public void SummaryBodyContradiction_IsNotHiddenByUnionOfMethods()
        {
            using (var a = Create("企业整体最终采用资产基础法。", "企业整体最终采用收益法。"))
            using (var b = Create("企业整体最终采用资产基础法。"))
            {
                Assert.AreEqual(ConsistencyStatus.FAIL, Execute(a, b).Results.Single().Status);
            }
        }

        [TestMethod]
        public void OrdinaryAdoption_IsNotPromotedToFinalOnMissingSide()
        {
            using (var a = Create("本次评估最终采用资产基础法。"))
            using (var b = Create("本次评估采用收益法。"))
            {
                Assert.AreEqual(ConsistencyStatus.WARNING, Execute(a, b).Results.Single().Status);
            }
        }

        [TestMethod]
        [DataRow("最终选用收益法。")]
        [DataRow("本次评估未最终采用收益法。")]
        [DataRow("本次评估可能最终采用收益法。")]
        [DataRow("本次评估最终采用资产基础法或收益法。")]
        [DataRow("本次评估按投资价值口径最终采用收益法。")]
        public void UncertainFinalAttribution_DoesNotProduceFinalFact(string text)
        {
            using (var file = Create(text))
            {
                var extraction = Read(file, "A");
                Assert.AreEqual(0, extraction.Facts.Count(item => item.MethodRole == EvaluationMethodRole.FinalAdopted));
                Assert.IsTrue(extraction.Statements.All(item => item.Confidence.Band == FactConfidenceBand.Low));
            }
        }

        [TestMethod]
        public void DifferentSubjects_AreNeverComparedAsFinalConflict()
        {
            using (var a = Create("企业整体最终采用资产基础法。"))
            using (var b = Create("机器设备最终采用收益法。"))
                Assert.IsTrue(Execute(a, b).Results.All(item => item.Status == ConsistencyStatus.WARNING));
        }

        [TestMethod]
        public void FinalAdoption_DoesNotWeakenExplicitNotApplicableProtection()
        {
            using (var a = Create("机器设备最终采用成本法。"))
            using (var b = Create("机器设备不适用成本法。"))
                Assert.AreEqual(ConsistencyStatus.FAIL, Execute(a, b).Results.Single().Status);
        }

        [TestMethod]
        public void FinalMethodRunSplit_DoesNotChangeExtraction()
        {
            using (var file = SyntheticDocxFile.Create(new Paragraph(
                new Run(new Text("本次评估最终选")), new Run(new Text("用资产基")), new Run(new Text("础法。")))))
            {
                var fact = Read(file, "A").Facts.Single();
                Assert.AreEqual(EvaluationMethodRole.FinalAdopted, fact.MethodRole);
                Assert.AreEqual(EvaluationMethodCode.AssetBasedApproach, fact.MethodCode);
            }
        }

        private static FactCheckExecutionEnvelope Execute(SyntheticDocxFile a, SyntheticDocxFile b)
        {
            return new EvaluationMethodChecker(new EvaluationMethodFactExtractor(), new EvaluationMethodComparer()).Execute(
                new V2DocumentAnalysisInput(new DocxDocumentReader().Read(a.FilePath), "DocumentA"),
                new V2DocumentAnalysisInput(new DocxDocumentReader().Read(b.FilePath), "DocumentB"));
        }

        private static Task<ReportCheckResult> Check(SyntheticDocxFile a, SyntheticDocxFile b)
        {
            return new ReportCheckService().CheckAsync(new ReportCheckRequest(a.FilePath, b.FilePath, null));
        }

        private static EvaluationMethodExtractionResult Read(SyntheticDocxFile file, string source)
        {
            return new EvaluationMethodFactExtractor().ExtractWithDiagnostics(new DocxDocumentReader().Read(file.FilePath), source);
        }

        private static SyntheticDocxFile Create(params string[] paragraphs)
        {
            return SyntheticDocxFile.Create(paragraphs.Select(text => new Paragraph(new Run(new Text(text)))).ToArray());
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return System.BitConverter.ToString(sha.ComputeHash(stream));
        }
    }
}
