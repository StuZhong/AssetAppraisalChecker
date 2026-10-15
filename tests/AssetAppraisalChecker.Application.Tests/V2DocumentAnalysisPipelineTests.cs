using System;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class V2DocumentAnalysisPipelineTests
    {
        private const string CommonScope = "本次列明的全部资产";

        [TestMethod]
        public void IdenticalDocumentObjects_ReturnPass()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report-a", CreateObjectText(6m, CommonScope)),
                CreateInput("report-b", CreateObjectText(6m, CommonScope)));

            Assert.AreEqual(ConsistencyStatus.PASS, result.StatusSummary);
            Assert.AreEqual(1, result.Results.Count);
            Assert.AreEqual(0, analyzer.CallCount);
        }

        [TestMethod]
        public void SemanticallyEquivalentDocumentObjects_ReturnPass()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report-a", CreateObjectText(6m, "全部供配电资产")),
                CreateInput("report-b", CreateObjectText(6m, "供配电资产全部范围")));

            Assert.AreEqual(ConsistencyStatus.PASS, result.StatusSummary);
            Assert.AreEqual(1, analyzer.CallCount);
            Assert.AreEqual("Scope", analyzer.LastFieldName);
        }

        [TestMethod]
        public void QuantityConflict_ReturnsFailWithoutCallingSemantic()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report-a", CreateObjectText(6m, CommonScope)),
                CreateInput("report-b", CreateObjectText(7m, CommonScope)));

            Assert.AreEqual(ConsistencyStatus.FAIL, result.StatusSummary);
            StringAssert.Contains(result.Results[0].Reason, "Quantity");
            Assert.AreEqual(0, analyzer.CallCount);
        }

        [TestMethod]
        public void SemanticFailure_ReturnsWarningWithoutGuessing()
        {
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(new ThrowingSemanticAnalyzer());

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report-a", CreateObjectText(6m, "全部供配电资产")),
                CreateInput("report-b", CreateObjectText(6m, "供配电资产全部范围")));

            Assert.AreEqual(ConsistencyStatus.WARNING, result.StatusSummary);
            StringAssert.Contains(result.Results[0].Reason, "Semantic");
        }

        [TestMethod]
        public void Analyze_ExecutesCompleteApplicationChainAndPreservesEvidence()
        {
            var analyzer = new RecordingSemanticAnalyzer();
            IV2DocumentAnalysisPipeline pipeline =
                new V2DocumentAnalysisPipeline(analyzer);
            const string firstText =
                "评估对象为虚构甲公司接收的供配电资产，涉及6家客户，范围为全部供配电资产。";
            const string secondText =
                "评估对象为虚构甲公司接收的供配电资产，涉及6家客户，范围为供配电资产全部范围。";

            V2AnalysisResult result = pipeline.Analyze(
                CreateInput("report-a", firstText),
                CreateInput("report-b", secondText));

            Assert.AreEqual(FactType.AssessmentObject, result.Results[0].FactType);
            Assert.AreEqual(ConsistencyStatus.PASS, result.Results[0].Status);
            Assert.AreEqual(1, analyzer.CallCount);
            Assert.IsTrue(ContainsEvidence(result, "report-a", firstText));
            Assert.IsTrue(ContainsEvidence(result, "report-b", secondText));
            Assert.IsTrue(ContainsEvidence(result, "Semantic", "Scope"));
            Assert.IsTrue(ContainsEvidence(
                result,
                "semantic-test-evidence",
                "semantic-test-evidence"));
        }

        private static V2DocumentAnalysisInput CreateInput(
            string sourceDocument,
            string text)
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                0,
                null,
                null,
                null,
                new[] { "评估对象" });
            var element = new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                location,
                0,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
            DocumentReadResult document = DocumentReadResult.Succeeded(
                new[] { element },
                1,
                0);

            return new V2DocumentAnalysisInput(document, sourceDocument);
        }

        private static string CreateObjectText(decimal quantity, string scope)
        {
            return string.Concat(
                "评估对象为虚构甲公司接收的供配电资产，涉及",
                quantity,
                "家客户，范围为",
                scope,
                "。");
        }

        private static bool ContainsEvidence(
            V2AnalysisResult result,
            string firstValue,
            string secondValue)
        {
            foreach (string evidence in result.Evidence)
            {
                if (evidence.IndexOf(firstValue, StringComparison.Ordinal) >= 0 &&
                    evidence.IndexOf(secondValue, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RecordingSemanticAnalyzer : ISemanticAnalyzer
        {
            public int CallCount { get; private set; }

            public string LastFieldName { get; private set; }

            public SemanticAnalysisResult Analyze(string textContent)
            {
                CallCount++;
                LastFieldName = ReadFieldName(textContent);

                return new SemanticAnalysisResult(
                    "application-pipeline-test",
                    "stable-test-representation",
                    0.95m,
                    0.95m,
                    new[] { "semantic-test-evidence" });
            }

            private static string ReadFieldName(string textContent)
            {
                const string prefix = "FieldName=";
                int lineEnd = textContent.IndexOf('\n');
                return lineEnd < 0
                    ? textContent.Substring(prefix.Length)
                    : textContent.Substring(prefix.Length, lineEnd - prefix.Length);
            }
        }

        private sealed class ThrowingSemanticAnalyzer : ISemanticAnalyzer
        {
            public SemanticAnalysisResult Analyze(string textContent)
            {
                throw new InvalidOperationException("模拟离线 Semantic 失败。");
            }
        }
    }
}
