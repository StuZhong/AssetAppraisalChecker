using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.CrossDocument.Tests
{
    [TestClass]
    public sealed class EvaluationMethodComparerTests
    {
        [TestMethod]
        public void Compare_SameSubjectSameMethod_Passes()
        {
            ConsistencyResult result = CompareSingle(Method(EvaluationMethodCode.CostApproach, "成本法"), Method(EvaluationMethodCode.CostApproach, "成本法", "explanation"));
            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
        }

        [TestMethod]
        public void Compare_CostAndReplacementCost_WarnsSymmetrically()
        {
            Assert.AreEqual(ConsistencyStatus.WARNING, CompareSingle(Method(EvaluationMethodCode.CostApproach, "成本法"), Method(EvaluationMethodCode.ReplacementCostMethod, "重置成本法", "explanation")).Status);
            Assert.AreEqual(ConsistencyStatus.WARNING, CompareSingle(Method(EvaluationMethodCode.ReplacementCostMethod, "重置成本法"), Method(EvaluationMethodCode.CostApproach, "成本法", "explanation")).Status);
        }

        [TestMethod]
        public void Compare_MarketAndIncome_Fails()
        {
            ConsistencyResult result = CompareSingle(Method(EvaluationMethodCode.MarketApproach, "市场法"), Method(EvaluationMethodCode.IncomeApproach, "收益法", "explanation"));
            Assert.AreEqual(ConsistencyStatus.FAIL, result.Status);
        }

        [TestMethod]
        public void Compare_OneSideMissing_Warns()
        {
            ConsistencyResult result = new EvaluationMethodComparer().Compare(
                new[] { Method(EvaluationMethodCode.CostApproach, "成本法") },
                new EvaluationMethodFact[0],
                new EvaluationMethodStatement[0],
                new[] { Statement(EvaluationMethodCode.Unknown, EvaluationMethodApplicability.CandidateUnknown, "explanation") })[0];
            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
        }

        [TestMethod]
        public void Compare_MultipleSubjects_ReturnsUniqueIdentitiesAndIsolatedEvidence()
        {
            EvaluationMethodFact reportBuilding = Method(EvaluationMethodCode.CostApproach, "成本法", "report", "房屋建筑物", EvaluationMethodSubjectKind.Building, "subject/v1:building");
            EvaluationMethodFact reportEquipment = Method(EvaluationMethodCode.MarketApproach, "市场法", "report", "机器设备", EvaluationMethodSubjectKind.MachineryEquipment, "subject/v1:machinery-equipment");
            EvaluationMethodFact explanationBuilding = Method(EvaluationMethodCode.CostApproach, "成本法", "explanation", "房屋建筑物", EvaluationMethodSubjectKind.Building, "subject/v1:building");
            EvaluationMethodFact explanationEquipment = Method(EvaluationMethodCode.IncomeApproach, "收益法", "explanation", "机器设备", EvaluationMethodSubjectKind.MachineryEquipment, "subject/v1:machinery-equipment");

            IReadOnlyList<ConsistencyResult> results = new EvaluationMethodComparer().Compare(
                new[] { reportBuilding, reportEquipment },
                new[] { explanationBuilding, explanationEquipment },
                new EvaluationMethodStatement[0],
                new EvaluationMethodStatement[0]);

            Assert.HasCount(2, results);
            Assert.AreEqual(2, results.Select(item => item.Identity).Distinct().Count());
            Assert.AreEqual(ConsistencyStatus.PASS, results.Single(item => item.Identity.SubjectKey == "subject/v1:building").Status);
            ConsistencyResult equipment = results.Single(item => item.Identity.SubjectKey == "subject/v1:machinery-equipment");
            Assert.AreEqual(ConsistencyStatus.FAIL, equipment.Status);
            Assert.IsFalse(equipment.Evidence.Any(item => item.Contains("房屋建筑物")));
        }

        [TestMethod]
        public void Compare_AdoptedVersusNotApplicable_Fails()
        {
            ConsistencyResult result = new EvaluationMethodComparer().Compare(
                new[] { Method(EvaluationMethodCode.CostApproach, "成本法") },
                new EvaluationMethodFact[0],
                new EvaluationMethodStatement[0],
                new[] { Statement(EvaluationMethodCode.CostApproach, EvaluationMethodApplicability.NotApplicable, "explanation") })[0];
            Assert.AreEqual(ConsistencyStatus.FAIL, result.Status);
        }

        private static ConsistencyResult CompareSingle(EvaluationMethodFact first, EvaluationMethodFact second)
        {
            return new EvaluationMethodComparer().Compare(
                new[] { first },
                new[] { second },
                new EvaluationMethodStatement[0],
                new EvaluationMethodStatement[0])[0];
        }

        private static EvaluationMethodFact Method(
            EvaluationMethodCode code,
            string name,
            string source = "report",
            string subject = "房屋建筑物",
            EvaluationMethodSubjectKind subjectKind = EvaluationMethodSubjectKind.Building,
            string subjectKey = "subject/v1:building")
        {
            EvaluationMethodFamily family = code == EvaluationMethodCode.MarketApproach
                ? EvaluationMethodFamily.Market
                : code == EvaluationMethodCode.IncomeApproach
                    ? EvaluationMethodFamily.Income
                    : EvaluationMethodFamily.Cost;
            EvaluationMethodSpecificity specificity = code == EvaluationMethodCode.ReplacementCostMethod
                ? EvaluationMethodSpecificity.SpecificMethod
                : EvaluationMethodSpecificity.BasicApproach;
            return new EvaluationMethodFact(
                name, code, family, specificity, subject, subjectKind, subjectKey,
                EvaluationMethodRole.Unspecified, "method-set/v1:" + source + ":0:0",
                EvaluationMethodSetCompleteness.Unknown, source, Location(), "采用" + name,
                new FactConfidence(FactConfidenceBand.High, 0.95m),
                new[] { subject + " | 采用" + name });
        }

        private static EvaluationMethodStatement Statement(
            EvaluationMethodCode code,
            EvaluationMethodApplicability applicability,
            string source)
        {
            return new EvaluationMethodStatement(
                "房屋建筑物", EvaluationMethodSubjectKind.Building, "subject/v1:building",
                code, applicability, source, Location(), "方法声明",
                new FactConfidence(FactConfidenceBand.Low, 0.40m),
                new[] { "方法声明" });
        }

        private static DocumentLocation Location()
        {
            return new DocumentLocation(DocumentPart.MainBody, 0, null, null, null, new[] { "评估方法" });
        }
    }
}
