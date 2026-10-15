using System.Collections.Generic;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Application.Tests
{
    [TestClass]
    public sealed class V2AnalysisPipelineTests
    {
        [TestMethod]
        public void SameFactCollections_ReturnPassSummary()
        {
            IV2AnalysisPipeline pipeline = CreatePipeline();

            V2AnalysisResult result = pipeline.Analyze(
                new[] { CreateAssessmentValueFact("report.docx", "评估净值170万元", "1700000") },
                new[] { CreateAssessmentValueFact("explanation.docx", "人民币壹佰柒拾万元整", "1700000") });

            Assert.AreEqual(ConsistencyStatus.PASS, result.StatusSummary);
            Assert.AreEqual(1, result.Results.Count);
            Assert.AreEqual(ConsistencyStatus.PASS, result.Results[0].Status);
        }

        [TestMethod]
        public void DifferentFactCollections_ReturnFailSummary()
        {
            IV2AnalysisPipeline pipeline = CreatePipeline();

            V2AnalysisResult result = pipeline.Analyze(
                new[] { CreateEvaluationMethodFact("report.docx", "评估方法：成本法", "成本法") },
                new[] { CreateEvaluationMethodFact("explanation.docx", "评估方法：收益法", "收益法") });

            Assert.AreEqual(ConsistencyStatus.FAIL, result.StatusSummary);
            Assert.AreEqual(1, result.Results.Count);
            Assert.AreEqual(ConsistencyStatus.FAIL, result.Results[0].Status);
        }

        [TestMethod]
        public void Result_ContainsAggregatedEvidence()
        {
            IV2AnalysisPipeline pipeline = CreatePipeline();

            V2AnalysisResult result = pipeline.Analyze(
                new[] { CreateAssessmentValueFact("report.docx", "评估净值170万元", "1700000") },
                new[] { CreateAssessmentValueFact("explanation.docx", "评估净值160万元", "1600000") });

            Assert.AreEqual(2, result.Evidence.Count);
            StringAssert.Contains(result.Evidence[0], "report.docx");
            StringAssert.Contains(result.Evidence[0], "评估净值170万元");
            StringAssert.Contains(result.Evidence[1], "explanation.docx");
            StringAssert.Contains(result.Evidence[1], "评估净值160万元");
        }

        [TestMethod]
        public void EmptyFactCollections_ReturnWarningSummary()
        {
            IV2AnalysisPipeline pipeline = CreatePipeline();

            V2AnalysisResult result = pipeline.Analyze(
                new BusinessFact[0],
                new BusinessFact[0]);

            Assert.AreEqual(ConsistencyStatus.WARNING, result.StatusSummary);
            Assert.AreEqual(0, result.Results.Count);
            Assert.AreEqual(0, result.Evidence.Count);
        }

        private static IV2AnalysisPipeline CreatePipeline()
        {
            return new V2AnalysisPipeline(new BasicFactComparer());
        }

        private static AssessmentValueFact CreateAssessmentValueFact(
            string sourceDocument,
            string originalText,
            string value)
        {
            return new AssessmentValueFact(
                sourceDocument,
                CreateLocation(),
                originalText,
                CreateConfidence(),
                value);
        }

        private static EvaluationMethodFact CreateEvaluationMethodFact(
            string sourceDocument,
            string originalText,
            string value)
        {
            return new EvaluationMethodFact(
                sourceDocument,
                CreateLocation(),
                originalText,
                CreateConfidence(),
                value);
        }

        private static DocumentLocation CreateLocation()
        {
            return new DocumentLocation(
                DocumentPart.MainBody,
                1,
                null,
                null,
                null,
                new[] { "评估结论" });
        }

        private static FactConfidence CreateConfidence()
        {
            return new FactConfidence(FactConfidenceBand.High, 0.95m);
        }
    }
}
