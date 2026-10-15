using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.CrossDocument.Tests
{
    [TestClass]
    public sealed class BasicFactComparerTests
    {
        [TestMethod]
        public void SameAssessmentValueFact_ReturnsPass()
        {
            ICrossDocumentComparer comparer = new BasicFactComparer();

            IReadOnlyList<ConsistencyResult> results = comparer.Compare(
                new[] { CreateAssessmentValueFact("report.docx", "170万元", "1700000") },
                new[] { CreateAssessmentValueFact("explanation.docx", "人民币壹佰柒拾万元整", "1700000") });

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(FactType.AssessmentValue, results[0].FactType);
            Assert.AreEqual(ConsistencyStatus.PASS, results[0].Status);
        }

        [TestMethod]
        public void DifferentEvaluationMethodFact_ReturnsFail()
        {
            ICrossDocumentComparer comparer = new BasicFactComparer();

            IReadOnlyList<ConsistencyResult> results = comparer.Compare(
                new[] { CreateEvaluationMethodFact("report.docx", "评估方法：成本法", "成本法") },
                new[] { CreateEvaluationMethodFact("explanation.docx", "评估方法：收益法", "收益法") });

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(FactType.EvaluationMethod, results[0].FactType);
            Assert.AreEqual(ConsistencyStatus.FAIL, results[0].Status);
        }

        [TestMethod]
        public void AssessmentObjectFactWithoutComparableValue_ReturnsWarning()
        {
            ICrossDocumentComparer comparer = new BasicFactComparer();

            IReadOnlyList<ConsistencyResult> results = comparer.Compare(
                new[] { CreateAssessmentObjectFact("report.docx", "评估对象：供配电资产", null) },
                new[] { CreateAssessmentObjectFact("explanation.docx", "评估对象：机器设备", "机器设备") });

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(FactType.AssessmentObject, results[0].FactType);
            Assert.AreEqual(ConsistencyStatus.WARNING, results[0].Status);
        }

        [TestMethod]
        public void Result_ContainsEvidenceFromBothDocuments()
        {
            ICrossDocumentComparer comparer = new BasicFactComparer();

            IReadOnlyList<ConsistencyResult> results = comparer.Compare(
                new[] { CreateAssessmentValueFact("report.docx", "评估净值170万元", "1700000") },
                new[] { CreateAssessmentValueFact("explanation.docx", "评估净值160万元", "1600000") });

            Assert.AreEqual(2, results[0].Evidence.Count);
            StringAssert.Contains(results[0].Evidence[0], "report.docx");
            StringAssert.Contains(results[0].Evidence[0], "评估净值170万元");
            StringAssert.Contains(results[0].Evidence[1], "explanation.docx");
            StringAssert.Contains(results[0].Evidence[1], "评估净值160万元");
        }

        [TestMethod]
        public void MissingSameTypeFact_ReturnsWarningInsteadOfPass()
        {
            ICrossDocumentComparer comparer = new BasicFactComparer();

            IReadOnlyList<ConsistencyResult> results = comparer.Compare(
                new[] { CreateAssessmentValueFact("report.docx", "评估净值170万元", "1700000") },
                new BusinessFact[0]);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(ConsistencyStatus.WARNING, results[0].Status);
            Assert.AreEqual(1, results[0].Evidence.Count);
        }

        [TestMethod]
        public void DifferentFactTypes_AreNotComparedAsEqualValues()
        {
            ICrossDocumentComparer comparer = new BasicFactComparer();

            IReadOnlyList<ConsistencyResult> results = comparer.Compare(
                new[] { CreateAssessmentValueFact("report.docx", "170万元", "1700000") },
                new[] { CreateEvaluationMethodFact("explanation.docx", "1700000", "1700000") });

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(ConsistencyStatus.WARNING, results[0].Status);
            Assert.AreEqual(ConsistencyStatus.WARNING, results[1].Status);
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

        private static AssessmentObjectFact CreateAssessmentObjectFact(
            string sourceDocument,
            string originalText,
            string value)
        {
            return new AssessmentObjectFact(
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
