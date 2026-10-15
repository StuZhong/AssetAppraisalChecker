using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Tests
{
    [TestClass]
    public sealed class EvaluationMethodFactTests
    {
        [TestMethod]
        public void Constructor_StructuredAdoptedFact_PreservesFrozenFields()
        {
            EvaluationMethodFact fact = CreateFact(0.95m);

            Assert.AreEqual(FactType.EvaluationMethod, fact.FactType);
            Assert.AreEqual("成本法", fact.Method);
            Assert.AreEqual(EvaluationMethodCode.CostApproach, fact.MethodCode);
            Assert.AreEqual(EvaluationMethodFamily.Cost, fact.MethodFamily);
            Assert.AreEqual(EvaluationMethodSpecificity.BasicApproach, fact.MethodSpecificity);
            Assert.AreEqual("房屋建筑物", fact.Subject);
            Assert.AreEqual("subject/v1:building", fact.NormalizedSubjectKey);
            Assert.AreEqual(EvaluationMethodRole.Unspecified, fact.MethodRole);
            Assert.AreEqual("method-set/v1:report:1:0", fact.MethodSetId);
            Assert.AreEqual(EvaluationMethodSetCompleteness.Complete, fact.SetCompleteness);
            Assert.AreEqual("成本法", fact.Value);
            Assert.IsTrue(fact.IsStructured);
        }

        [TestMethod]
        public void Constructor_Evidence_IsReadOnlySnapshot()
        {
            var evidence = new List<string> { "房屋建筑物 | 采用成本法" };
            EvaluationMethodFact fact = CreateFact(0.95m, evidence);
            evidence[0] = "changed";

            Assert.AreEqual("房屋建筑物 | 采用成本法", fact.Evidence[0]);
            Assert.ThrowsExactly<NotSupportedException>(
                () => ((IList<string>)fact.Evidence).Add("x"));
        }

        [TestMethod]
        [DataRow("-0.01")]
        [DataRow("1.01")]
        public void Constructor_InvalidConfidence_Fails(string value)
        {
            decimal confidence = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateFact(confidence));
        }

        private static EvaluationMethodFact CreateFact(
            decimal confidence,
            IEnumerable<string> evidence = null)
        {
            return new EvaluationMethodFact(
                "成本法",
                EvaluationMethodCode.CostApproach,
                EvaluationMethodFamily.Cost,
                EvaluationMethodSpecificity.BasicApproach,
                "房屋建筑物",
                EvaluationMethodSubjectKind.Building,
                "subject/v1:building",
                EvaluationMethodRole.Unspecified,
                "method-set/v1:report:1:0",
                EvaluationMethodSetCompleteness.Complete,
                "report",
                new DocumentLocation(DocumentPart.MainBody, 1, null, null, null, new[] { "评估方法" }),
                "采用成本法",
                new FactConfidence(FactConfidenceBand.High, confidence),
                evidence ?? new[] { "房屋建筑物 | 采用成本法" });
        }
    }
}
