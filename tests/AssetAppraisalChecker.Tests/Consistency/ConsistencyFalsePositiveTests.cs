using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class ConsistencyFalsePositiveTests
    {
        [TestMethod]
        public void Check_AppraisedOriginalAndNetValues_AreNotCompared()
        {
            AssertSeparated(
                ConsistencyTestOccurrenceFactory.Create(1700000m, category: ValueCategory.AppraisedOriginalValue, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, category: ValueCategory.AppraisedNetValue, order: 1));
        }

        [TestMethod]
        public void Check_TaxExcludedAndIncludedValues_AreNotCompared()
        {
            AssertSeparated(
                ConsistencyTestOccurrenceFactory.Create(1700000m, taxBasis: TaxBasis.TaxExcluded, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1921000m, taxBasis: TaxBasis.TaxIncluded, order: 1));
        }

        [TestMethod]
        public void Check_UnspecifiedAndSpecifiedTaxBasis_AreNotMerged()
        {
            AssertSeparated(
                ConsistencyTestOccurrenceFactory.Create(1700000m, taxBasis: TaxBasis.Unspecified, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1921000m, taxBasis: TaxBasis.TaxIncluded, order: 1));
        }

        [TestMethod]
        public void Check_BookAndAppraisedValues_AreNotCompared()
        {
            AssertSeparated(
                ConsistencyTestOccurrenceFactory.Create(1000000m, category: ValueCategory.BookValue, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1200000m, category: ValueCategory.AppraisedValue, order: 1));
        }

        [TestMethod]
        public void Check_ReplacementCostAndAppraisedNetValue_AreNotCompared()
        {
            AssertSeparated(
                ConsistencyTestOccurrenceFactory.Create(1800000m, category: ValueCategory.ReplacementCost, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, category: ValueCategory.AppraisedNetValue, order: 1));
        }

        [TestMethod]
        public void Check_SingleAssetAndProjectTotal_AreNotCompared()
        {
            AssertSeparated(
                ConsistencyTestOccurrenceFactory.Create(500000m, scope: Scope.SingleAsset, order: 0),
                ConsistencyTestOccurrenceFactory.Create(5000000m, scope: Scope.ProjectTotal, order: 1));
        }

        [TestMethod]
        public void Check_CalculationCaseAndProjectTotal_AreNotCompared()
        {
            AssertSeparated(
                ConsistencyTestOccurrenceFactory.Create(1620000m, scope: Scope.CalculationCase, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1700000m, scope: Scope.ProjectTotal, order: 1));
        }

        [TestMethod]
        public void Check_UnspecifiedAndSpecifiedScope_AreNotMerged()
        {
            AssertSeparated(
                ConsistencyTestOccurrenceFactory.Create(1620000m, scope: Scope.Unspecified, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1700000m, scope: Scope.ProjectTotal, order: 1));
        }

        [TestMethod]
        public void Check_FourCoreFields_FormFourGroupsWithoutConflict()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(385800m, category: ValueCategory.AppraisedOriginalValue, taxBasis: TaxBasis.TaxExcluded, order: 0),
                ConsistencyTestOccurrenceFactory.Create(381900m, category: ValueCategory.AppraisedNetValue, taxBasis: TaxBasis.TaxExcluded, order: 1),
                ConsistencyTestOccurrenceFactory.Create(425500m, category: ValueCategory.AppraisedOriginalValue, taxBasis: TaxBasis.TaxIncluded, order: 2),
                ConsistencyTestOccurrenceFactory.Create(421300m, category: ValueCategory.AppraisedNetValue, taxBasis: TaxBasis.TaxIncluded, order: 3)
            };

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(4, result.ComparisonGroups.Count);
            Assert.AreEqual(4, result.ComparisonGroups.Select(group => group.ComparisonKey).Distinct().Count());
            Assert.AreEqual(0, result.Issues.Count);
        }

        private static void AssertSeparated(params MoneyOccurrence[] occurrences)
        {
            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }
    }
}
