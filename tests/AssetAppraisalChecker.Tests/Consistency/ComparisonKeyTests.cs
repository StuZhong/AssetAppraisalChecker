using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class ComparisonKeyTests
    {
        [TestMethod]
        public void From_SectionCategoriesDiffer_ReturnsEqualKeys()
        {
            var summary = new FieldIdentity(
                ValueCategory.AppraisedNetValue,
                TaxBasis.TaxExcluded,
                Scope.ProjectTotal,
                SectionCategory.ReportSummary);
            var table = new FieldIdentity(
                ValueCategory.AppraisedNetValue,
                TaxBasis.TaxExcluded,
                Scope.ProjectTotal,
                SectionCategory.ResultTable);

            Assert.AreEqual(
                ComparisonKey.From(summary, ComparisonSubjectKey.Project),
                ComparisonKey.From(table, ComparisonSubjectKey.Project));
            Assert.AreEqual(
                ComparisonKey.From(summary, ComparisonSubjectKey.Project).GetHashCode(),
                ComparisonKey.From(table, ComparisonSubjectKey.Project).GetHashCode());
        }

        [TestMethod]
        public void Equality_TaxBasisDiffers_ReturnsFalse()
        {
            var excluded = new ComparisonKey(ValueCategory.AppraisedNetValue, TaxBasis.TaxExcluded, Scope.ProjectTotal, ComparisonSubjectKey.Project);
            var included = new ComparisonKey(ValueCategory.AppraisedNetValue, TaxBasis.TaxIncluded, Scope.ProjectTotal, ComparisonSubjectKey.Project);

            Assert.AreNotEqual(excluded, included);
        }

        [TestMethod]
        public void Equality_ScopeDiffers_ReturnsFalse()
        {
            var single = new ComparisonKey(ValueCategory.AppraisedNetValue, TaxBasis.TaxExcluded, Scope.SingleAsset, ComparisonSubjectKey.Project);
            var total = new ComparisonKey(ValueCategory.AppraisedNetValue, TaxBasis.TaxExcluded, Scope.ProjectTotal, ComparisonSubjectKey.Project);

            Assert.AreNotEqual(single, total);
        }

        [TestMethod]
        public void Equality_ValueCategoryDiffers_ReturnsFalse()
        {
            var original = new ComparisonKey(ValueCategory.AppraisedOriginalValue, TaxBasis.TaxExcluded, Scope.ProjectTotal, ComparisonSubjectKey.Project);
            var net = new ComparisonKey(ValueCategory.AppraisedNetValue, TaxBasis.TaxExcluded, Scope.ProjectTotal, ComparisonSubjectKey.Project);

            Assert.AreNotEqual(original, net);
        }

        [TestMethod]
        public void Equality_SubjectDiffers_ReturnsFalse()
        {
            var assetA = new ComparisonKey(
                ValueCategory.AppraisedNetValue,
                TaxBasis.TaxExcluded,
                Scope.Unspecified,
                new ComparisonSubjectKey(SubjectKind.Asset, "测试设备A"));
            var assetB = new ComparisonKey(
                ValueCategory.AppraisedNetValue,
                TaxBasis.TaxExcluded,
                Scope.Unspecified,
                new ComparisonSubjectKey(SubjectKind.Asset, "测试设备B"));

            Assert.AreNotEqual(assetA, assetB);
        }
    }
}
