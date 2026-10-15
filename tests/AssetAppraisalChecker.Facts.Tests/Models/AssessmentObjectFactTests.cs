using System;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Tests.Models
{
    [TestClass]
    public sealed class AssessmentObjectFactTests
    {
        [TestMethod]
        public void Constructor_WithValidValues_CreatesAssessmentObjectFact()
        {
            AssessmentObjectFact fact = CreateFact();

            Assert.IsNotNull(fact);
            Assert.AreEqual(FactType.AssessmentObject, fact.FactType);
        }

        [TestMethod]
        public void Constructor_PreservesStructuredFields()
        {
            AssessmentObjectFact fact = CreateFact();

            Assert.AreEqual("机器设备", fact.AssetType);
            Assert.AreEqual("虚构产权持有人甲公司", fact.Owner);
            Assert.AreEqual("资产转让", fact.EconomicAction);
            Assert.AreEqual(12m, fact.Quantity);
            Assert.AreEqual("纳入清单的全部设备", fact.Scope);
        }

        [TestMethod]
        public void Constructor_UsesFactConfidenceUnitIntervalValidation()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                CreateFact(new FactConfidence(FactConfidenceBand.Low, -0.01m)));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                CreateFact(new FactConfidence(FactConfidenceBand.High, 1.01m)));
        }

        [TestMethod]
        public void Constructor_PreservesSourceInformation()
        {
            DocumentLocation location = CreateLocation();
            FactConfidence confidence = new FactConfidence(FactConfidenceBand.High, 0.95m);

            AssessmentObjectFact fact = new AssessmentObjectFact(
                "report.docx",
                location,
                "评估对象为虚构产权持有人甲公司拟转让的12台机器设备。",
                confidence,
                "机器设备",
                "虚构产权持有人甲公司",
                "资产转让",
                12m,
                "纳入清单的全部设备");

            Assert.AreEqual("report.docx", fact.SourceDocument);
            Assert.AreSame(location, fact.SourceLocation);
            Assert.AreEqual(
                "评估对象为虚构产权持有人甲公司拟转让的12台机器设备。",
                fact.OriginalText);
            Assert.AreSame(confidence, fact.Confidence);
            Assert.AreEqual(0.95m, fact.Confidence.Score);
        }

        [TestMethod]
        public void Constructor_RejectsMissingStructuredTextFields()
        {
            Assert.ThrowsExactly<ArgumentException>(() => CreateFact(assetType: " "));
            Assert.ThrowsExactly<ArgumentException>(() => CreateFact(owner: null));
            Assert.ThrowsExactly<ArgumentException>(() => CreateFact(economicAction: string.Empty));
            Assert.ThrowsExactly<ArgumentException>(() => CreateFact(scope: " "));
        }

        [TestMethod]
        public void Constructor_RejectsNegativeQuantity()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateFact(quantity: -1m));
        }

        private static AssessmentObjectFact CreateFact(
            FactConfidence confidence = null,
            string assetType = "机器设备",
            string owner = "虚构产权持有人甲公司",
            string economicAction = "资产转让",
            decimal quantity = 12m,
            string scope = "纳入清单的全部设备")
        {
            return new AssessmentObjectFact(
                "report.docx",
                CreateLocation(),
                "评估对象为虚构产权持有人甲公司拟转让的12台机器设备。",
                confidence ?? new FactConfidence(FactConfidenceBand.High, 0.95m),
                assetType,
                owner,
                economicAction,
                quantity,
                scope);
        }

        private static DocumentLocation CreateLocation()
        {
            return new DocumentLocation(
                DocumentPart.MainBody,
                3,
                null,
                null,
                null,
                new[] { "评估对象" });
        }
    }
}
