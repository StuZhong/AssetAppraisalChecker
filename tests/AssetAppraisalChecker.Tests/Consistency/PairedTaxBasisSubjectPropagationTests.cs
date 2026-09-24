using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class PairedTaxBasisSubjectPropagationTests
    {
        private readonly MoneyOccurrenceExtractor extractor = new MoneyOccurrenceExtractor();

        [TestMethod]
        public void Extract_ExplicitPairedTaxValues_PropagatesOneAssetToAllIncludedValues()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                PairedValues("测试设备甲", "100", "壹佰", "113", "壹佰壹拾叁"));

            List<MoneyOccurrence> excluded = TaxOccurrences(occurrences, TaxBasis.TaxExcluded);
            List<MoneyOccurrence> included = TaxOccurrences(occurrences, TaxBasis.TaxIncluded);

            Assert.AreEqual(2, excluded.Count);
            Assert.AreEqual(2, included.Count);
            Assert.IsTrue(excluded.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.IsTrue(included.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.IsTrue(included.All(item =>
                item.ComparisonSubjectKey.Equals(excluded[0].ComparisonSubjectKey)));
            Assert.IsTrue(included.All(item => item.SubjectEvidence.Any(evidence =>
                evidence.Contains("金额值未参与主体判断"))));
        }

        [TestMethod]
        public void Check_TwoRepeatedPairedStructures_CreateSeparateTaxBasisGroups()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                PairedValues("测试设备甲", "100", "壹佰", "113", "壹佰壹拾叁"),
                PairedValues("测试设备甲", "100", "壹佰", "113", "壹佰壹拾叁"));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.SkippedOccurrenceCount);
            Assert.AreEqual(0, result.Issues.Count);
            CollectionAssert.AreEquivalent(
                new[] { TaxBasis.TaxExcluded, TaxBasis.TaxIncluded },
                result.ComparisonGroups.Select(group => group.ComparisonKey.TaxBasis).ToArray());
        }

        [TestMethod]
        public void Check_IncludedValuesConflict_ProducesTaxIncludedMoneyConflict()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                PairedValues("测试设备甲", "100", "壹佰", "113", "壹佰壹拾叁"),
                PairedValues("测试设备甲", "100", "壹佰", "114", "壹佰壹拾肆"));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueType.MoneyConflict, result.Issues[0].IssueType);
            Assert.AreEqual(TaxBasis.TaxIncluded, result.Issues[0].ComparisonKey.TaxBasis);
        }

        [TestMethod]
        public void Check_DifferentExcludedAndIncludedAmounts_NeverConflictAcrossTaxBasis()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                PairedValues("测试设备甲", "100", "壹佰", "113", "壹佰壹拾叁"),
                PairedValues("测试设备甲", "100", "壹佰", "113", "壹佰壹拾叁"));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.IsFalse(result.ComparisonGroups.Any(group =>
                group.Occurrences.Select(item => item.FieldIdentity.TaxBasis).Distinct().Count() > 1));
        }

        [TestMethod]
        public void Extract_DifferentValueCategory_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "测试设备甲不含税评估价值为100万元，含税评估净值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(ValueCategory.AppraisedNetValue, target.FieldIdentity.ValueCategory);
            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_TwoCompetingAssetSubjects_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "测试设备甲不含税评估价值为100万元，测试设备乙不含税评估价值为110万元，含税评估价值为124万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_IncludedValueInAnotherSentence_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "测试设备甲不含税评估价值为100万元。含税评估价值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_IncludedValueWithoutSource_DoesNotPropagate()
        {
            MoneyOccurrence occurrence = ExtractParagraphs(
                "含税评估价值为113万元。").Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_BookValuePair_DoesNotUseAppraisedValuePropagation()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "测试设备甲不含税账面价值为90万元，含税账面价值为100万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(ValueCategory.BookValue, target.FieldIdentity.ValueCategory);
            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_ProjectSource_DoesNotPropagateAsAsset()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "本项目不含税评估价值为100万元，含税评估价值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_UnknownSourceSubject_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "不含税评估价值为100万元，含税评估价值为113万元。");

            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_BlockingBusinessFieldBetweenPairs_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "测试设备甲不含税评估价值为100万元，账面价值为90万元，含税评估价值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_InsertedAssetTextWithoutMoney_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "测试设备甲不含税评估价值为100万元，测试设备乙另行说明，含税评估价值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_NetValuePair_PropagatesUniqueAssetSubject()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                PairedNetValues("虚构设备甲", "100", "壹佰", "113", "壹佰壹拾叁"));

            List<MoneyOccurrence> excluded = TaxOccurrences(occurrences, TaxBasis.TaxExcluded);
            List<MoneyOccurrence> included = TaxOccurrences(occurrences, TaxBasis.TaxIncluded);

            Assert.AreEqual(2, excluded.Count);
            Assert.AreEqual(2, included.Count);
            Assert.IsTrue(occurrences.All(item =>
                item.FieldIdentity.ValueCategory == ValueCategory.AppraisedNetValue));
            Assert.IsTrue(occurrences.All(item =>
                item.FieldIdentity.Scope == Scope.Subtotal));
            Assert.IsTrue(included.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.IsTrue(included.All(item =>
                item.ComparisonSubjectKey.Equals(excluded[0].ComparisonSubjectKey)));
            Assert.IsTrue(included.All(item => item.SubjectEvidence.Any(evidence =>
                evidence.Contains("TaxExcluded AppraisedNetValue") &&
                evidence.Contains("金额值未参与主体判断"))));
        }

        [TestMethod]
        public void Extract_NetValuePairWithCompetingAssetSubjects_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "虚构设备甲不含税评估净值为100万元，虚构设备乙不含税评估净值为110万元，含税评估净值为124万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_NetValuePairAcrossParagraphs_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "虚构设备甲不含税评估净值为100万元。",
                "含税评估净值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_NetValuesWithUnspecifiedTaxBasis_DoNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "虚构设备甲评估净值为100万元，评估净值为100万元。");

            Assert.IsTrue(occurrences.All(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.Unspecified));
            Assert.IsFalse(occurrences.Any(HasPropagationEvidence));
        }

        [TestMethod]
        public void Extract_MixedAppraisedNetAndGeneralValue_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "虚构设备甲不含税评估净值为100万元，含税评估价值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(ValueCategory.AppraisedValue, target.FieldIdentity.ValueCategory);
            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_NetAndBookValuePair_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "虚构设备甲不含税评估净值为100万元，含税账面价值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(ValueCategory.BookValue, target.FieldIdentity.ValueCategory);
            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_AppraisedOriginalValuePair_RemainsUnsupported()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "虚构设备甲不含税评估原值为100万元，含税评估原值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(ValueCategory.AppraisedOriginalValue, target.FieldIdentity.ValueCategory);
            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_EqualNetAmountsWithoutPairedTaxLabels_DoNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "虚构设备甲评估净值为100万元，另一处评估净值为100万元。");

            Assert.IsFalse(occurrences.Any(HasPropagationEvidence));
        }

        [TestMethod]
        public void Extract_AdjacentNetValuesWithUnsafeNarrative_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "虚构设备甲不含税评估净值为100万元，另有业务说明，含税评估净值为113万元。");

            MoneyOccurrence target = occurrences.Single(item =>
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded);

            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Check_PropagatedNetValuesRemainSeparatedByTaxBasis()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                PairedNetValues("虚构设备甲", "100", "壹佰", "113", "壹佰壹拾叁"),
                PairedNetValues("虚构设备甲", "100", "壹佰", "113", "壹佰壹拾叁"));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.SkippedOccurrenceCount);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.IsFalse(result.ComparisonGroups.Any(group =>
                group.Occurrences.Select(item => item.FieldIdentity.TaxBasis).Distinct().Count() > 1));
            CollectionAssert.AreEquivalent(
                new[] { TaxBasis.TaxExcluded, TaxBasis.TaxIncluded },
                result.ComparisonGroups.Select(group => group.ComparisonKey.TaxBasis).ToArray());
        }

        [TestMethod]
        public void Extract_NetValuePairWithoutConfirmedAssetSubject_DoesNotPropagate()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "不含税评估净值为100万元，含税评估净值为113万元。");

            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Unknown));
            Assert.IsFalse(occurrences.Any(HasPropagationEvidence));
        }

        private IReadOnlyList<MoneyOccurrence> ExtractParagraphs(params string[] texts)
        {
            return extractor.Extract(texts.Select((text, index) => new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    index,
                    null,
                    null,
                    null,
                    new string[0]),
                index,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty))));
        }

        private static string PairedValues(
            string subject,
            string excludedArabic,
            string excludedChinese,
            string includedArabic,
            string includedChinese)
        {
            return string.Format(
                "{0}不含税评估价值为{1}万元（大写：人民币{2}万元整），含税评估价值为{3}万元（大写：人民币{4}万元整）。",
                subject,
                excludedArabic,
                excludedChinese,
                includedArabic,
                includedChinese);
        }

        private static string PairedNetValues(
            string subject,
            string excludedArabic,
            string excludedChinese,
            string includedArabic,
            string includedChinese)
        {
            return string.Format(
                "{0}不含税评估净值合计为{1}万元（大写：人民币{2}万元整），含税评估净值合计为{3}万元（大写：人民币{4}万元整）。",
                subject,
                excludedArabic,
                excludedChinese,
                includedArabic,
                includedChinese);
        }

        private static bool HasPropagationEvidence(MoneyOccurrence occurrence)
        {
            return occurrence.SubjectEvidence.Any(evidence =>
                evidence.Contains("金额值未参与主体判断"));
        }

        private static List<MoneyOccurrence> TaxOccurrences(
            IEnumerable<MoneyOccurrence> occurrences,
            TaxBasis taxBasis)
        {
            return occurrences
                .Where(item => item.FieldIdentity.TaxBasis == taxBasis)
                .ToList();
        }
    }
}
