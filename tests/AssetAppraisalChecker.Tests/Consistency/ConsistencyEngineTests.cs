using System;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class ConsistencyEngineTests
    {
        [TestMethod]
        public void Check_SameKeyAndSameAmount_ReturnsNoIssue()
        {
            ConsistencyCheckResult result = Check(1700000m, 1700000m);

            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_SameKeyAndDifferentAmounts_ReturnsOneSeriousConflict()
        {
            ConsistencyCheckResult result = Check(1700000m, 1600000m);

            Assert.AreEqual(1, result.Issues.Count);
            Issue issue = result.Issues[0];
            Assert.AreEqual(IssueType.MoneyConflict, issue.IssueType);
            Assert.AreEqual(IssueSeverity.Serious, issue.Severity);
            Assert.AreEqual(100000m, issue.Difference);
            Assert.AreEqual(2, issue.Occurrences.Count);
        }

        [TestMethod]
        public void Check_DefaultOneYuanTolerance_HalfYuanDifferenceIsConsistent()
        {
            ConsistencyCheckResult result = Check(1000000m, 1000000.50m);

            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_DefaultOneYuanTolerance_TwoYuanDifferenceConflicts()
        {
            ConsistencyCheckResult result = Check(1000000m, 1000002m);

            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(2m, result.Issues[0].Difference);
        }

        [TestMethod]
        public void Check_EquivalentChineseAndArabicExpressions_UsesNormalizedAmountOnly()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(
                    1700000m,
                    rawText: "人民币壹佰柒拾万元整",
                    order: 0,
                    numberFormat: MoneyNumberFormat.ChineseFinancial),
                ConsistencyTestOccurrenceFactory.Create(1700000m, rawText: "170万元", order: 1),
                ConsistencyTestOccurrenceFactory.Create(1700000m, rawText: "1,700,000元", order: 2)
            };

            Assert.AreEqual(0, new ConsistencyEngine().Check(occurrences).Issues.Count);
        }

        [TestMethod]
        public void Check_TwoConflictingOccurrences_DoesNotDeclareAnOutlier()
        {
            Issue issue = Check(1700000m, 1600000m).Issues.Single();

            Assert.AreEqual(0, issue.SuspectedOutlierOccurrences.Count);
            StringAssert.Contains(issue.SuggestedAction, "人工核对");
            Assert.IsFalse(issue.SuggestedAction.Contains("修改"));
        }

        [TestMethod]
        public void Check_ThreeOccurrencesWithTwoToOneMajority_MarksMinorityAsSuspectedOutlier()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1600000m, rawText: "160万元", order: 0),
                ConsistencyTestOccurrenceFactory.Create(1700000m, rawText: "170万元", order: 1),
                ConsistencyTestOccurrenceFactory.Create(1700000m, rawText: "人民币壹佰柒拾万元整", order: 2)
            };

            Issue issue = new ConsistencyEngine().Check(occurrences).Issues.Single();

            Assert.AreEqual(1, issue.SuspectedOutlierOccurrences.Count);
            Assert.AreEqual("160万元", issue.SuspectedOutlierOccurrences[0].RawText);
        }

        [TestMethod]
        public void Check_ThreeDifferentClusters_HasNoSuspectedOutlier()
        {
            Issue issue = Check(1700000m, 1600000m, 1500000m).Issues.Single();

            Assert.AreEqual(0, issue.SuspectedOutlierOccurrences.Count);
        }

        [TestMethod]
        public void Check_TwoToTwoTie_HasNoSuspectedOutlier()
        {
            Issue issue = Check(1700000m, 1700000m, 1600000m, 1600000m).Issues.Single();

            Assert.AreEqual(0, issue.SuspectedOutlierOccurrences.Count);
        }

        [TestMethod]
        public void Check_ClusteringUsesToleranceForMajority()
        {
            Issue issue = Check(1700000m, 1700000.50m, 1600000m).Issues.Single();

            Assert.AreEqual(1, issue.SuspectedOutlierOccurrences.Count);
            Assert.AreEqual(1600000m, issue.SuspectedOutlierOccurrences[0].NormalizedAmountYuan);
        }

        [TestMethod]
        public void Check_SectionCategoriesDiffer_UsesOneGroupAndFindsOutlier()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1700000m, section: SectionCategory.ReportSummary, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, section: SectionCategory.ValuationConclusion, order: 1),
                ConsistencyTestOccurrenceFactory.Create(1700000m, section: SectionCategory.ResultTable, order: 2, isTableCell: true)
            };

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(3, result.ComparisonGroups[0].Occurrences.Count);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(1600000m, result.Issues[0].SuspectedOutlierOccurrences.Single().NormalizedAmountYuan);
            StringAssert.Contains(result.Issues[0].Reason, "报告摘要");
            StringAssert.Contains(result.Issues[0].Reason, "评估结论");
            StringAssert.Contains(result.Issues[0].Reason, "评估结果表");
        }

        [TestMethod]
        public void Check_ReportSummaryAndValuationConclusion_AreCompared()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1700000m, section: SectionCategory.ReportSummary, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, section: SectionCategory.ValuationConclusion, order: 1)
            };

            Assert.AreEqual(1, new ConsistencyEngine().Check(occurrences).Issues.Count);
        }

        [TestMethod]
        public void Check_ValuationConclusionAndResultTable_AreCompared()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1700000m, section: SectionCategory.ValuationConclusion, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, section: SectionCategory.ResultTable, order: 1, isTableCell: true)
            };

            Assert.AreEqual(1, new ConsistencyEngine().Check(occurrences).Issues.Count);
        }

        [TestMethod]
        public void Check_ParagraphAndTableCell_AreCompared()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1700000m, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, order: 1, isTableCell: true)
            };

            Assert.AreEqual(1, new ConsistencyEngine().Check(occurrences).Issues.Count);
        }

        [TestMethod]
        public void Check_UnknownOccurrences_AreSkippedWithoutSeriousIssue()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1700000m, category: ValueCategory.Unknown, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, category: ValueCategory.Unknown, order: 1)
            };

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(0, result.ComparisonGroups.Count);
            Assert.AreEqual(2, result.SkippedOccurrenceCount);
        }

        [TestMethod]
        public void Check_LowConfidenceOccurrences_AreSkippedWithoutSeriousIssue()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1700000m, confidence: ClassificationConfidence.Low, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, confidence: ClassificationConfidence.Low, order: 1)
            };

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(2, result.SkippedOccurrenceCount);
        }

        [TestMethod]
        public void Check_DirectHighFieldWithWeakAuxiliaryContext_RemainsComparableAsReminder()
        {
            var elements = new[]
            {
                CreateParagraphWithWeakTaxContext("测试设备A评估净值170万元", 0),
                CreateParagraphWithWeakTaxContext("测试设备A评估净值160万元", 1)
            };
            var occurrences = new MoneyOccurrenceExtractor().Extract(elements);

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.IsTrue(occurrences.All(item => item.ValueCategoryConfidence == ClassificationConfidence.High));
            Assert.IsTrue(occurrences.All(item => item.TaxBasisConfidence == ClassificationConfidence.Low));
            Assert.IsTrue(occurrences.All(item => item.ClassificationConfidence == ClassificationConfidence.Low));
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueSeverity.Reminder, result.Issues[0].Severity);
            Assert.AreEqual(0, result.SkippedOccurrenceCount);
        }

        [TestMethod]
        public void Check_MediumConfidenceCoreAmount_ProducesNormalNotSerious()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1700000m, confidence: ClassificationConfidence.Medium, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, confidence: ClassificationConfidence.Medium, order: 1)
            };

            Issue issue = new ConsistencyEngine().Check(occurrences).Issues.Single();

            Assert.AreEqual(IssueSeverity.Normal, issue.Severity);
            Assert.AreEqual(ClassificationConfidence.Medium, issue.Confidence);
        }

        [TestMethod]
        public void Check_NonCoreAmountConflict_ProducesNormal()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1000000m, category: ValueCategory.ReplacementCost, order: 0),
                ConsistencyTestOccurrenceFactory.Create(900000m, category: ValueCategory.ReplacementCost, order: 1)
            };

            Assert.AreEqual(IssueSeverity.Normal, new ConsistencyEngine().Check(occurrences).Issues.Single().Severity);
        }

        [TestMethod]
        public void Check_SingleOccurrence_ReturnsNoIssue()
        {
            ConsistencyCheckResult result = new ConsistencyEngine().Check(new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1700000m)
            });

            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_EmptyCollection_ReturnsEmptyResult()
        {
            ConsistencyCheckResult result = new ConsistencyEngine().Check(new AssetAppraisalChecker.Core.BusinessFields.MoneyOccurrence[0]);

            Assert.AreEqual(0, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(0, result.SkippedOccurrenceCount);
        }

        [TestMethod]
        public void Check_NullCollection_Throws()
        {
            try
            {
                new ConsistencyEngine().Check(null);
                Assert.Fail("null 输入应抛出 ArgumentNullException。");
            }
            catch (ArgumentNullException)
            {
            }
        }

        [TestMethod]
        public void Check_ZeroAmounts_AreValidAndConsistent()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(0m, rawText: "0元", order: 0),
                ConsistencyTestOccurrenceFactory.Create(0m, rawText: "0.00万元", order: 1)
            };

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(2, result.ComparisonGroups[0].Occurrences.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_RepeatedRuns_AreDeterministicAndStateless()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1600000m, rawText: "160万元", order: 0),
                ConsistencyTestOccurrenceFactory.Create(1700000m, rawText: "170万元", order: 1),
                ConsistencyTestOccurrenceFactory.Create(1700000m, rawText: "人民币壹佰柒拾万元整", order: 2)
            };
            var engine = new ConsistencyEngine();

            Issue first = engine.Check(occurrences).Issues.Single();
            Issue second = engine.Check(occurrences.Reverse()).Issues.Single();

            Assert.AreEqual(first.IssueId, second.IssueId);
            Assert.AreEqual(first.Difference, second.Difference);
            Assert.AreEqual(first.Reason, second.Reason);
            Assert.AreEqual(first.Severity, second.Severity);
            CollectionAssert.AreEqual(
                first.SuspectedOutlierOccurrences.Select(item => item.RawText).ToArray(),
                second.SuspectedOutlierOccurrences.Select(item => item.RawText).ToArray());
        }

        [TestMethod]
        public void Check_ReasonAndSuggestedAction_AreExplanatoryAndNeutral()
        {
            Issue issue = Check(1700000m, 1600000m).Issues.Single();

            StringAssert.Contains(issue.Reason, "标准化金额");
            StringAssert.Contains(issue.Reason, "金额簇");
            StringAssert.Contains(issue.FieldName, "不含税评估净值");
            StringAssert.Contains(issue.SuggestedAction, "确认最终正确金额");
            Assert.IsFalse(issue.SuggestedAction.Contains("改为"));
        }

        [TestMethod]
        public void Check_MultipleClusters_DifferenceIsMaximumSpread()
        {
            Issue issue = Check(1700000m, 1600000m, 1500000m).Issues.Single();

            Assert.AreEqual(200000m, issue.Difference);
        }

        [TestMethod]
        public void Check_NonTransitiveToleranceChain_DoesNotMergeBeyondMaximumSpread()
        {
            Issue issue = Check(1000000m, 1000000.75m, 1000001.50m).Issues.Single();

            Assert.AreEqual(1.50m, issue.Difference);
        }

        private static ConsistencyCheckResult Check(params decimal[] amounts)
        {
            return new ConsistencyEngine().Check(amounts.Select((amount, index) =>
                ConsistencyTestOccurrenceFactory.Create(amount, order: index)));
        }

        private static DocumentElement CreateParagraphWithWeakTaxContext(string text, int order)
        {
            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(DocumentPart.MainBody, order, null, null, null, new string[0]),
                order,
                string.Empty,
                new DocumentNearbyContext("不含税口径", text, string.Empty));
        }
    }
}
