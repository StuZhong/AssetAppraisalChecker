using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class SubjectIdentityTests
    {
        private readonly MoneyOccurrenceExtractor extractor = new MoneyOccurrenceExtractor();

        [TestMethod]
        public void Extract_ExplicitProjectPhrase_AssignsStableProjectSubject()
        {
            MoneyOccurrence occurrence = ExtractParagraphs(
                "本项目不含税评估净值170万元").Single();

            Assert.AreEqual(SubjectKind.Project, occurrence.SubjectKind);
            Assert.AreEqual("PROJECT", occurrence.SubjectId);
            Assert.AreEqual(ClassificationConfidence.High, occurrence.SubjectConfidence);
            Assert.AreEqual(Scope.ProjectTotal, occurrence.FieldIdentity.Scope);
            Assert.IsTrue(occurrence.SubjectEvidence.Any(item => item.Contains("本项目")));
        }

        [TestMethod]
        [DataRow("本次评估项目不含税评估净值170万元")]
        [DataRow("评估对象整体不含税评估净值170万元")]
        [DataRow("项目合计不含税评估净值170万元")]
        [DataRow("评估结果合计不含税评估净值170万元")]
        [DataRow("评估总值为170万元")]
        [DataRow("评估结果汇总为170万元")]
        [DataRow("全部评估对象的评估净值为170万元")]
        [DataRow("整体评估结果为170万元")]
        public void Extract_DeterministicProjectPhrases_AssignProjectSubject(string text)
        {
            MoneyOccurrence occurrence = ExtractParagraphs(text).Single();

            Assert.AreEqual(SubjectKind.Project, occurrence.SubjectKind);
            Assert.AreEqual("PROJECT", occurrence.SubjectId);
            Assert.AreEqual(ClassificationConfidence.High, occurrence.SubjectConfidence);
        }

        [TestMethod]
        public void Extract_AmbiguousConclusion_KeepsUnknownSubject()
        {
            MoneyOccurrence occurrence = ExtractParagraphs(
                "经评估，评估净值170万元").Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
            Assert.AreEqual(string.Empty, occurrence.SubjectId);
            Assert.AreEqual(ClassificationConfidence.Low, occurrence.SubjectConfidence);
        }

        [TestMethod]
        public void Extract_ExplicitAssetPrefix_AssignsAssetSubjectAndEvidence()
        {
            MoneyOccurrence occurrence = ExtractParagraphs(
                "测试设备A评估净值50万元").Single();

            Assert.AreEqual(SubjectKind.Asset, occurrence.SubjectKind);
            Assert.AreEqual("测试设备A", occurrence.SubjectId);
            Assert.AreEqual(ClassificationConfidence.High, occurrence.SubjectConfidence);
            Assert.IsTrue(occurrence.SubjectEvidence.Any(item => item.Contains("当前金额所在子句")));
        }

        [TestMethod]
        [DataRow("资产评估价值100万元")]
        [DataRow("资产价值100万元")]
        [DataRow("评估资产的评估价值100万元")]
        [DataRow("评估对象的评估价值100万元")]
        [DataRow("评估对象资产价值100万元")]
        [DataRow("全部资产的评估价值100万元")]
        [DataRow("纳入评估范围的机器设备评估价值100万元")]
        public void Extract_GenericAssetPhrases_DoNotCreateAssetSubject(string text)
        {
            MoneyOccurrence occurrence = ExtractParagraphs(text).Single();

            Assert.AreNotEqual(SubjectKind.Asset, occurrence.SubjectKind);
            Assert.AreNotEqual("资产", occurrence.SubjectId);
        }

        [TestMethod]
        public void Check_DistinctProjectNameRows_UseStableSeparatedSubjects()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(
                CreateProjectNameTable());

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(3, occurrences.Count);
            Assert.AreEqual(SubjectKind.Asset, occurrences[0].SubjectKind);
            Assert.AreEqual("项目A", occurrences[0].SubjectId);
            Assert.AreEqual(SubjectKind.Asset, occurrences[1].SubjectKind);
            Assert.AreEqual("项目B", occurrences[1].SubjectId);
            Assert.AreEqual(SubjectKind.Project, occurrences[2].SubjectKind);
            Assert.AreEqual("PROJECT", occurrences[2].SubjectId);
            Assert.AreEqual(3, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Extract_AmbiguousProjectNameColumnValue_RemainsUnknown()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                CreateTwoColumnTable("项目名称", "测试设备A")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_AssetSubject_NormalizesFullWidthAndRepeatedWhitespace()
        {
            MoneyOccurrence occurrence = ExtractParagraphs(
                "测试　设备A  评估净值50万元").Single();

            Assert.AreEqual("测试 设备A", occurrence.SubjectId);
        }

        [TestMethod]
        public void Check_DifferentParagraphAssets_AreNeverCompared()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "测试设备A评估净值50万元",
                "测试设备B评估净值80万元");

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_SameParagraphAssetAndSameAmount_IsConsistent()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "测试设备A评估净值50万元",
                "测试设备A评估净值50万元");

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(2, result.ComparisonGroups[0].Occurrences.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_SameParagraphAssetAndDifferentAmount_Conflicts()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "测试设备A评估净值50万元",
                "测试设备A评估净值55万元");

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(50000m, result.Issues[0].Difference);
            Assert.AreEqual(0, result.Issues[0].SuspectedOutlierOccurrences.Count);
        }

        [TestMethod]
        public void Check_UnknownSubjectAndUnspecifiedScope_IsFailClosed()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = ExtractParagraphs(
                "评估净值170万元",
                "评估净值160万元");

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(0, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(2, result.SkippedOccurrenceCount);
        }

        [TestMethod]
        public void Check_ProjectSubjectAndUnspecifiedScope_CanCompare()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(1700000m, scope: Scope.Unspecified, order: 0),
                ConsistencyTestOccurrenceFactory.Create(1600000m, scope: Scope.Unspecified, order: 1)
            };

            Assert.AreEqual(1, new ConsistencyEngine().Check(occurrences).Issues.Count);
        }

        [TestMethod]
        public void Check_AssetSubjectAndUnspecifiedScope_CanCompare()
        {
            var occurrences = new[]
            {
                AssetOccurrence(500000m, "测试设备A", order: 0),
                AssetOccurrence(550000m, "测试设备A", order: 1)
            };

            Assert.AreEqual(1, new ConsistencyEngine().Check(occurrences).Issues.Count);
        }

        [TestMethod]
        public void Check_DifferentSubjectsWithSameAmount_DoNotMerge()
        {
            var occurrences = new[]
            {
                AssetOccurrence(500000m, "测试设备A", order: 0),
                AssetOccurrence(500000m, "测试设备B", order: 1)
            };

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_SameSubjectButDifferentValueCategory_DoNotCompare()
        {
            var occurrences = new[]
            {
                AssetOccurrence(600000m, "测试设备A", ValueCategory.AppraisedOriginalValue, order: 0),
                AssetOccurrence(500000m, "测试设备A", ValueCategory.AppraisedNetValue, order: 1)
            };

            AssertSeparated(occurrences);
        }

        [TestMethod]
        public void Check_SameSubjectButDifferentTaxBasis_DoNotCompare()
        {
            var occurrences = new[]
            {
                AssetOccurrence(500000m, "测试设备A", taxBasis: TaxBasis.TaxExcluded, order: 0),
                AssetOccurrence(565000m, "测试设备A", taxBasis: TaxBasis.TaxIncluded, order: 1)
            };

            AssertSeparated(occurrences);
        }

        [TestMethod]
        public void Check_SameSubjectButDifferentExplicitScope_DoNotCompare()
        {
            var occurrences = new[]
            {
                AssetOccurrence(500000m, "测试设备A", scope: Scope.SingleAsset, order: 0),
                AssetOccurrence(5000000m, "测试设备A", scope: Scope.ProjectTotal, order: 1)
            };

            AssertSeparated(occurrences);
        }

        [TestMethod]
        public void Check_SameSubjectAcrossSections_IsCompared()
        {
            var occurrences = new[]
            {
                AssetOccurrence(500000m, "测试设备A", section: SectionCategory.ReportSummary, order: 0),
                AssetOccurrence(550000m, "测试设备A", section: SectionCategory.ResultTable, order: 1)
            };

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(1, result.Issues.Count);
        }

        [TestMethod]
        public void Check_LowSubjectConfidence_IsFailClosed()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(
                    500000m,
                    scope: Scope.Unspecified,
                    order: 0,
                    subjectKind: SubjectKind.Asset,
                    subjectId: "测试设备A",
                    subjectConfidence: ClassificationConfidence.Low),
                ConsistencyTestOccurrenceFactory.Create(
                    550000m,
                    scope: Scope.Unspecified,
                    order: 1,
                    subjectKind: SubjectKind.Asset,
                    subjectId: "测试设备A",
                    subjectConfidence: ClassificationConfidence.Low)
            };

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(2, result.SkippedOccurrenceCount);
        }

        [TestMethod]
        public void Check_MediumSubjectConfidence_ConflictIsNormalNotSerious()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(
                    500000m,
                    scope: Scope.Unspecified,
                    order: 0,
                    subjectKind: SubjectKind.Asset,
                    subjectId: "测试设备A",
                    subjectConfidence: ClassificationConfidence.Medium),
                ConsistencyTestOccurrenceFactory.Create(
                    550000m,
                    scope: Scope.Unspecified,
                    order: 1,
                    subjectKind: SubjectKind.Asset,
                    subjectId: "测试设备A",
                    subjectConfidence: ClassificationConfidence.Medium)
            };

            Issue issue = new ConsistencyEngine().Check(occurrences).Issues.Single();

            Assert.AreEqual(IssueSeverity.Normal, issue.Severity);
            Assert.AreEqual(ClassificationConfidence.Medium, issue.Confidence);
        }

        [TestMethod]
        public void Check_UnknownSubjectWithExplicitScope_IsStillFailClosed()
        {
            var occurrences = new[]
            {
                ConsistencyTestOccurrenceFactory.Create(
                    1700000m,
                    scope: Scope.ProjectTotal,
                    order: 0,
                    subjectKind: SubjectKind.Unknown),
                ConsistencyTestOccurrenceFactory.Create(
                    1600000m,
                    scope: Scope.ProjectTotal,
                    order: 1,
                    subjectKind: SubjectKind.Unknown)
            };

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(0, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(2, result.SkippedOccurrenceCount);
        }

        [TestMethod]
        public void Extract_AssetNameTableColumn_AssignsHighAssetSubject()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                CreateTwoColumnTable("资产名称", "测试设备A")).Single();

            Assert.AreEqual(SubjectKind.Asset, occurrence.SubjectKind);
            Assert.AreEqual("测试设备A", occurrence.SubjectId);
            Assert.AreEqual(ClassificationConfidence.High, occurrence.SubjectConfidence);
            Assert.IsTrue(occurrence.SubjectEvidence.Any(item => item.Contains("资产名称")));
        }

        [TestMethod]
        [DataRow("序号")]
        [DataRow("数量")]
        [DataRow("规格型号")]
        [DataRow("金额")]
        [DataRow("评估原值")]
        [DataRow("评估净值")]
        [DataRow("备注")]
        [DataRow("项目名称")]
        public void Extract_ProhibitedTableColumn_DoesNotBecomeSubject(string header)
        {
            MoneyOccurrence occurrence = extractor.Extract(
                CreateTwoColumnTable(header, "测试设备A")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void ComparisonSubjectKey_EqualValuesHaveStableEqualityAndHash()
        {
            var first = new ComparisonSubjectKey(SubjectKind.Asset, "测试设备A");
            var second = new ComparisonSubjectKey(SubjectKind.Asset, "测试设备A");

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [TestMethod]
        public void ComparisonSubjectKey_ConstructorNormalizesWhitespace()
        {
            var key = new ComparisonSubjectKey(SubjectKind.Asset, "  测试　设备A   ");

            Assert.AreEqual("测试 设备A", key.NormalizedSubjectId);
        }

        private IReadOnlyList<MoneyOccurrence> ExtractParagraphs(params string[] texts)
        {
            return extractor.Extract(texts.Select((text, index) => new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(DocumentPart.MainBody, index, null, null, null, new string[0]),
                index,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty))));
        }

        private static MoneyOccurrence AssetOccurrence(
            decimal amount,
            string subjectId,
            ValueCategory category = ValueCategory.AppraisedNetValue,
            TaxBasis taxBasis = TaxBasis.TaxExcluded,
            Scope scope = Scope.Unspecified,
            SectionCategory section = SectionCategory.ValuationConclusion,
            int order = 0)
        {
            return ConsistencyTestOccurrenceFactory.Create(
                amount,
                category,
                taxBasis,
                scope,
                section,
                order: order,
                subjectKind: SubjectKind.Asset,
                subjectId: subjectId);
        }

        private static void AssertSeparated(IEnumerable<MoneyOccurrence> occurrences)
        {
            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);
            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        private static IReadOnlyList<DocumentElement> CreateTwoColumnTable(
            string possibleSubjectHeader,
            string possibleSubjectValue)
        {
            return new[]
            {
                Cell(0, 0, 0, possibleSubjectHeader),
                Cell(1, 0, 1, "评估净值"),
                Cell(2, 1, 0, possibleSubjectValue),
                Cell(3, 1, 1, "50万元")
            };
        }

        private static IReadOnlyList<DocumentElement> CreateProjectNameTable()
        {
            return new[]
            {
                Cell(0, 0, 0, "项目名称"),
                Cell(1, 0, 1, "评估净值"),
                Cell(2, 1, 0, "项目A"),
                Cell(3, 1, 1, "50万元"),
                Cell(4, 2, 0, "项目B"),
                Cell(5, 2, 1, "80万元"),
                Cell(6, 3, 0, "合计"),
                Cell(7, 3, 1, "130万元")
            };
        }

        private static DocumentElement Cell(int order, int row, int cell, string text)
        {
            return new DocumentElement(
                DocumentElementType.TableCell,
                text,
                new DocumentLocation(DocumentPart.MainBody, null, 0, row, cell, new string[0]),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
