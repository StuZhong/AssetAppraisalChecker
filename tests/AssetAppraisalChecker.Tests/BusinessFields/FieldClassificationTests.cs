using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BusinessFields
{
    [TestClass]
    public sealed class FieldClassificationTests
    {
        private readonly MoneyOccurrenceExtractor _extractor = new MoneyOccurrenceExtractor();

        [TestMethod]
        [DataRow("评估原值100万元", ValueCategory.AppraisedOriginalValue)]
        [DataRow("评估值原值100万元", ValueCategory.AppraisedOriginalValue)]
        [DataRow("评估净值100万元", ValueCategory.AppraisedNetValue)]
        [DataRow("评估价值100万元", ValueCategory.AppraisedValue)]
        [DataRow("评估结果100万元", ValueCategory.AppraisedValue)]
        [DataRow("账面价值100万元", ValueCategory.BookValue)]
        [DataRow("账面值100万元", ValueCategory.BookValue)]
        [DataRow("账面原值100万元", ValueCategory.BookOriginalValue)]
        [DataRow("账面净值100万元", ValueCategory.BookNetValue)]
        [DataRow("重置成本100万元", ValueCategory.ReplacementCost)]
        [DataRow("重置全价100万元", ValueCategory.ReplacementCost)]
        [DataRow("评估增值额100万元", ValueCategory.Increment)]
        [DataRow("评估减值额100万元", ValueCategory.Decrement)]
        [DataRow("建安工程费100万元", ValueCategory.ConstructionCost)]
        [DataRow("建筑安装工程费100万元", ValueCategory.ConstructionCost)]
        [DataRow("其他费用100万元", ValueCategory.OtherCost)]
        [DataRow("资金成本100万元", ValueCategory.CapitalCost)]
        [DataRow("税费100万元", ValueCategory.Tax)]
        [DataRow("增值税额100万元", ValueCategory.Tax)]
        public void Extract_DirectBusinessLabel_UsesSpecificValueCategory(
            string text,
            ValueCategory expectedCategory)
        {
            MoneyOccurrence occurrence = ExtractSingle(text);

            Assert.AreEqual(expectedCategory, occurrence.FieldIdentity.ValueCategory);
            Assert.AreEqual(ClassificationConfidence.High, occurrence.ClassificationConfidence);
            Assert.IsTrue(occurrence.ClassificationEvidence.Count > 0);
        }

        [TestMethod]
        [DataRow("含税评估净值100万元", TaxBasis.TaxIncluded)]
        [DataRow("含增值税评估净值100万元", TaxBasis.TaxIncluded)]
        [DataRow("不含税评估净值100万元", TaxBasis.TaxExcluded)]
        [DataRow("不含增值税评估净值100万元", TaxBasis.TaxExcluded)]
        public void Extract_TaxPhrases_ClassifiesTaxBasis(string text, TaxBasis expectedTaxBasis)
        {
            MoneyOccurrence occurrence = ExtractSingle(text);

            Assert.AreEqual(expectedTaxBasis, occurrence.FieldIdentity.TaxBasis);
        }

        [TestMethod]
        public void Extract_TaxExcludedPhrase_DoesNotMatchContainedTaxIncludedPhrase()
        {
            MoneyOccurrence occurrence = ExtractSingle("不含税评估原值38.58万元");

            Assert.AreEqual(TaxBasis.TaxExcluded, occurrence.FieldIdentity.TaxBasis);
            Assert.AreNotEqual(TaxBasis.TaxIncluded, occurrence.FieldIdentity.TaxBasis);
        }

        [TestMethod]
        public void Extract_TwoAmountsInOneSentence_UsesEachCandidateClause()
        {
            const string text = "不含税评估原值为180万元，不含税评估净值为170万元。";
            var occurrences = Extract(text);

            Assert.AreEqual(2, occurrences.Count);
            AssertOccurrence(occurrences[0], 1800000m, ValueCategory.AppraisedOriginalValue, TaxBasis.TaxExcluded);
            AssertOccurrence(occurrences[1], 1700000m, ValueCategory.AppraisedNetValue, TaxBasis.TaxExcluded);
            foreach (MoneyOccurrence occurrence in occurrences)
            {
                Assert.AreEqual(
                    occurrence.RawText,
                    text.Substring(occurrence.TextStartIndex, occurrence.TextLength));
            }
        }

        [TestMethod]
        public void Extract_ThreeAmountsInOneSentence_KeepsFieldAssociation()
        {
            var occurrences = Extract("账面价值100万元，评估价值120万元，增值额20万元。");

            Assert.AreEqual(3, occurrences.Count);
            Assert.AreEqual(ValueCategory.BookValue, occurrences[0].FieldIdentity.ValueCategory);
            Assert.AreEqual(ValueCategory.AppraisedValue, occurrences[1].FieldIdentity.ValueCategory);
            Assert.AreEqual(ValueCategory.Increment, occurrences[2].FieldIdentity.ValueCategory);
        }

        [TestMethod]
        public void Extract_ChineseAmounts_UsesSameLocalClassificationRules()
        {
            var occurrences = Extract(
                "评估原值人民币壹佰捌拾万元整，评估净值人民币壹佰柒拾万元整。");

            Assert.AreEqual(2, occurrences.Count);
            AssertOccurrence(occurrences[0], 1800000m, ValueCategory.AppraisedOriginalValue, TaxBasis.Unspecified);
            AssertOccurrence(occurrences[1], 1700000m, ValueCategory.AppraisedNetValue, TaxBasis.Unspecified);
        }

        [TestMethod]
        public void Extract_NonstandardChineseAmount_PreservesPhase1WarningAndFieldClassification()
        {
            MoneyOccurrence occurrence = ExtractSingle("评估净值壹百柒拾万元");

            Assert.AreEqual(1700000m, occurrence.NormalizedAmountYuan);
            Assert.AreEqual(ValueCategory.AppraisedNetValue, occurrence.FieldIdentity.ValueCategory);
            Assert.AreEqual(
                AssetAppraisalChecker.Core.Money.MoneyParseStatus.SuccessWithWarning,
                occurrence.MoneyParseStatus);
            Assert.IsTrue(occurrence.ParseWarnings.Count > 0);
        }

        [TestMethod]
        public void Extract_FourTaxAndValueCombinations_ProducesDistinctFieldIdentities()
        {
            var occurrences = new List<MoneyOccurrence>();
            occurrences.AddRange(Extract("不含税评估原值38.58万元"));
            occurrences.AddRange(Extract("不含税评估净值38.19万元"));
            occurrences.AddRange(Extract("含税评估原值42.55万元"));
            occurrences.AddRange(Extract("含税评估净值42.13万元"));

            CollectionAssert.AreEqual(
                new[] { 385800m, 381900m, 425500m, 421300m },
                occurrences.Select(item => item.NormalizedAmountYuan).ToArray());
            Assert.AreEqual(4, occurrences.Select(item => item.FieldIdentity).Distinct().Count());
        }

        [TestMethod]
        public void Extract_UnlabeledAmount_KeepsUnknownWithLowConfidence()
        {
            MoneyOccurrence occurrence = ExtractSingle("金额为170万元。");

            Assert.AreEqual(ValueCategory.Unknown, occurrence.FieldIdentity.ValueCategory);
            Assert.AreEqual(ClassificationConfidence.Low, occurrence.ClassificationConfidence);
            Assert.IsTrue(occurrence.ClassificationEvidence.Any(item => item.Contains("Unknown")));
        }

        [TestMethod]
        [DataRow("报告摘要", SectionCategory.ReportSummary)]
        [DataRow("评估结论", SectionCategory.ValuationConclusion)]
        [DataRow("评估结果汇总表", SectionCategory.ResultTable)]
        [DataRow("计算过程", SectionCategory.CalculationProcess)]
        [DataRow("评估说明", SectionCategory.Explanation)]
        [DataRow("附件", SectionCategory.Appendix)]
        [DataRow("其他章节", SectionCategory.Other)]
        public void Extract_HeadingPath_ClassifiesSection(
            string heading,
            SectionCategory expectedSection)
        {
            MoneyOccurrence occurrence = ExtractSingle(
                "评估净值170万元",
                new[] { "资产评估报告", heading });

            Assert.AreEqual(expectedSection, occurrence.FieldIdentity.SectionCategory);
        }

        [TestMethod]
        [DataRow("单项资产评估值100万元", Scope.SingleAsset)]
        [DataRow("评估值小计100万元", Scope.Subtotal)]
        [DataRow("项目评估值100万元", Scope.ProjectTotal)]
        public void Extract_ExplicitScopeKeywords_ClassifiesConservatively(string text, Scope expectedScope)
        {
            Assert.AreEqual(expectedScope, ExtractSingle(text).FieldIdentity.Scope);
        }

        [TestMethod]
        public void Extract_CalculationCaseHeading_OverridesFinalValueSoundingText()
        {
            MoneyOccurrence occurrence = ExtractSingle(
                "项目评估值180万元",
                new[] { "评估说明", "测算案例" });

            Assert.AreEqual(Scope.CalculationCase, occurrence.FieldIdentity.Scope);
            Assert.AreEqual(SectionCategory.CalculationProcess, occurrence.FieldIdentity.SectionCategory);
        }

        [TestMethod]
        public void Extract_OnlyNearbyFieldEvidence_UsesLowConfidence()
        {
            DocumentElement element = CreateElement(
                "金额为170万元",
                new string[0],
                new DocumentNearbyContext("评估净值", "金额为170万元", string.Empty));

            MoneyOccurrence occurrence = _extractor.Extract(new[] { element }).Single();

            Assert.AreEqual(ValueCategory.AppraisedNetValue, occurrence.FieldIdentity.ValueCategory);
            Assert.AreEqual(ClassificationConfidence.Low, occurrence.ClassificationConfidence);
            Assert.IsTrue(occurrence.ClassificationEvidence.Any(item => item.Contains("前一有效元素")));
        }

        [TestMethod]
        public void Extract_FollowingTotalElement_DoesNotLeakScopeBackward()
        {
            DocumentElement element = CreateElement(
                "评估净值100万元",
                new string[0],
                new DocumentNearbyContext(string.Empty, "评估净值100万元", "合计"));

            MoneyOccurrence occurrence = _extractor.Extract(new[] { element }).Single();

            Assert.AreEqual(Scope.Unspecified, occurrence.FieldIdentity.Scope);
            Assert.IsFalse(occurrence.ClassificationEvidence.Any(item =>
                item.Contains("后一有效元素") && item.Contains("Scope")));
        }

        [TestMethod]
        [DataRow("评估价值合计100万元", Scope.Subtotal)]
        [DataRow("项目合计评估价值100万元", Scope.ProjectTotal)]
        [DataRow("评估结果合计100万元", Scope.ProjectTotal)]
        [DataRow("评估总值100万元", Scope.ProjectTotal)]
        public void Extract_TotalPhrases_DistinguishSubtotalAndProjectTotal(
            string text,
            Scope expectedScope)
        {
            Assert.AreEqual(expectedScope, ExtractSingle(text).FieldIdentity.Scope);
        }

        [TestMethod]
        public void Extract_FinalAggregateInSummary_UsesCombinedProjectEvidence()
        {
            MoneyOccurrence occurrence = ExtractSingle(
                "评估价值不含税合计100万元",
                new[] { "报告摘要" });

            Assert.AreEqual(SubjectKind.Project, occurrence.SubjectKind);
            Assert.AreEqual(Scope.ProjectTotal, occurrence.FieldIdentity.Scope);
        }

        private MoneyOccurrence ExtractSingle(string text, IReadOnlyList<string> headingPath = null)
        {
            return Extract(text, headingPath).Single();
        }

        private IReadOnlyList<MoneyOccurrence> Extract(
            string text,
            IReadOnlyList<string> headingPath = null)
        {
            return _extractor.Extract(new[]
            {
                CreateElement(
                    text,
                    headingPath ?? new string[0],
                    new DocumentNearbyContext(string.Empty, text, string.Empty))
            });
        }

        private static DocumentElement CreateElement(
            string text,
            IEnumerable<string> headingPath,
            DocumentNearbyContext nearbyContext)
        {
            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(DocumentPart.MainBody, 0, null, null, null, headingPath),
                0,
                string.Empty,
                nearbyContext);
        }

        private static void AssertOccurrence(
            MoneyOccurrence occurrence,
            decimal expectedAmount,
            ValueCategory expectedCategory,
            TaxBasis expectedTaxBasis)
        {
            Assert.AreEqual(expectedAmount, occurrence.NormalizedAmountYuan);
            Assert.AreEqual(expectedCategory, occurrence.FieldIdentity.ValueCategory);
            Assert.AreEqual(expectedTaxBasis, occurrence.FieldIdentity.TaxBasis);
        }
    }
}
