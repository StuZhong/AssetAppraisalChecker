using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BusinessFields
{
    [TestClass]
    public sealed class LineNameTableSubjectTests
    {
        private const string AuditedHeader = "名称（分项/线路名称）";

        private readonly MoneyOccurrenceExtractor extractor = new MoneyOccurrenceExtractor();

        [TestMethod]
        public void Extract_BoundedComplexLineNameHeader_AssignsAssetSubject()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable(AuditedHeader, "虚构线路甲", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Asset, occurrence.SubjectKind);
            Assert.AreEqual("虚构线路甲", occurrence.SubjectId);
            Assert.AreEqual(ClassificationConfidence.High, occurrence.SubjectConfidence);
            Assert.IsTrue(occurrence.SubjectEvidence.Any(item =>
                item.Contains("线路名称主体列")));
        }

        [TestMethod]
        public void Extract_TwoDetailRows_AssignsDistinctLineSubjects()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(new[]
            {
                Cell(0, 0, 0, AuditedHeader),
                Cell(1, 0, 1, "评估净值"),
                Cell(2, 1, 0, "虚构线路甲"),
                Cell(3, 1, 1, "50万元"),
                Cell(4, 2, 0, "虚构线路乙"),
                Cell(5, 2, 1, "50万元")
            });

            Assert.AreEqual(2, occurrences.Count);
            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.AreEqual(2, occurrences.Select(item => item.SubjectId).Distinct().Count());
        }

        [TestMethod]
        public void Extract_FourMoneyCellsInSameRow_ShareOneLineSubject()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(
                FourValueRows("虚构线路甲"));

            Assert.AreEqual(4, occurrences.Count);
            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.AreEqual(1, occurrences.Select(item => item.SubjectId).Distinct().Count());
        }

        [TestMethod]
        public void Check_MultipleLineRows_RemainStrictlySeparated()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(
                FourValueRows("虚构线路甲", "虚构线路乙"));
            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(8, occurrences.Count);
            Assert.AreEqual(2, occurrences.Select(item => item.SubjectId).Distinct().Count());
            Assert.AreEqual(8, result.ComparisonGroups.Count);
            Assert.IsTrue(result.ComparisonGroups.All(group => group.Occurrences.Count == 1));
            Assert.IsFalse(result.ComparisonGroups.Any(group =>
                group.Occurrences.Select(item => item.Location.RowIndex).Distinct().Count() > 1));
            Assert.IsFalse(result.ComparisonGroups.Any(group =>
                group.Occurrences.Select(item => item.FieldIdentity.TaxBasis).Distinct().Count() > 1));
        }

        [TestMethod]
        public void Extract_LegacyExactNameHeader_RemainsSupported()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable("名称", "虚构线路甲", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Asset, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_LegacySuffixHeader_RemainsSupported()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable("虚构资产名称", "虚构资产甲", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Asset, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_UnsupportedQualifiedNameHeader_DoesNotActivateLineRule()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable("其他业务名称", "虚构线路甲", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_UnboundedLineNameText_DoesNotActivateLineRule()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable("其他线路名称说明", "虚构线路甲", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_CompetingSubjectSemanticsInHeader_FailsClosed()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable("项目名称（分项/线路名称）", "虚构线路甲", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_RepeatedLineNameSemanticInHeader_FailsClosed()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable("名称（线路名称/线路名称）", "虚构线路甲", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_TwoPossibleLineSubjectHeaderColumns_FailsClosed()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(new[]
            {
                Cell(0, 0, 0, AuditedHeader),
                Cell(1, 0, 1, "名称（备用/线路名称）"),
                Cell(2, 0, 2, "评估净值"),
                Cell(3, 1, 0, "虚构线路甲"),
                Cell(4, 1, 1, "虚构线路乙"),
                Cell(5, 1, 2, "50万元")
            });

            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_SecondTextCandidateInDetailRow_FailsClosed()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(new[]
            {
                Cell(0, 0, 0, AuditedHeader),
                Cell(1, 0, 1, "说明"),
                Cell(2, 0, 2, "评估净值"),
                Cell(3, 1, 0, "虚构线路甲"),
                Cell(4, 1, 1, "虚构备用文本"),
                Cell(5, 1, 2, "50万元")
            });

            MoneyOccurrence amount = occurrences.Single(item => item.Location.CellIndex == 2);
            Assert.AreEqual(SubjectKind.Unknown, amount.SubjectKind);
        }

        [TestMethod]
        public void Extract_EmptyLineSubjectCell_FailsClosed()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable(AuditedHeader, string.Empty, "50万元")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_NumericLineSubjectCell_FailsClosed()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable(AuditedHeader, "123", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_MoneyLineSubjectCell_FailsClosed()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(
                SingleValueTable(AuditedHeader, "50万元", "60万元"));

            MoneyOccurrence amount = occurrences.Single(item => item.Location.CellIndex == 1);
            Assert.AreEqual(SubjectKind.Unknown, amount.SubjectKind);
        }

        [TestMethod]
        [DataRow("台")]
        [DataRow("千米")]
        [DataRow("kV")]
        public void Extract_UnitOnlyLineSubjectCell_FailsClosed(string unit)
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable(AuditedHeader, unit, "50万元")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        [DataRow("合计")]
        [DataRow("总计")]
        [DataRow("小计")]
        public void Extract_AggregateWordNeverBecomesLineAssetSubject(string aggregateWord)
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(
                SingleValueTable(AuditedHeader, aggregateWord, "50万元"));

            Assert.IsFalse(occurrences.Any(item => item.SubjectKind == SubjectKind.Asset));
        }

        [TestMethod]
        public void Extract_ProjectTotalRow_RemainsProject()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable(AuditedHeader, "总计", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Project, occurrence.SubjectKind);
            Assert.AreEqual(Scope.ProjectTotal, occurrence.FieldIdentity.Scope);
        }

        [TestMethod]
        public void Extract_LineSubject_DoesNotPropagateAcrossRows()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(new[]
            {
                Cell(0, 0, 0, AuditedHeader),
                Cell(1, 0, 1, "评估净值"),
                Cell(2, 1, 0, "虚构线路甲"),
                Cell(3, 1, 1, "50万元"),
                Cell(4, 2, 0, string.Empty),
                Cell(5, 2, 1, "50万元")
            });

            Assert.AreEqual(SubjectKind.Asset, occurrences[0].SubjectKind);
            Assert.AreEqual(SubjectKind.Unknown, occurrences[1].SubjectKind);
        }

        [TestMethod]
        public void Extract_ParagraphPath_RemainsUnaffected()
        {
            var paragraph = new DocumentElement(
                DocumentElementType.Paragraph,
                "本项目不含税评估净值为100万元。",
                new DocumentLocation(
                    DocumentPart.MainBody,
                    0,
                    null,
                    null,
                    null,
                    new string[0]),
                0,
                string.Empty,
                new DocumentNearbyContext(
                    string.Empty,
                    "本项目不含税评估净值为100万元。",
                    string.Empty));

            MoneyOccurrence occurrence = extractor.Extract(new[] { paragraph }).Single();

            Assert.AreEqual(SubjectKind.Project, occurrence.SubjectKind);
        }

        private static IReadOnlyList<DocumentElement> SingleValueTable(
            string header,
            string subject,
            string amount)
        {
            return new[]
            {
                Cell(0, 0, 0, header),
                Cell(1, 0, 1, "评估净值"),
                Cell(2, 1, 0, subject),
                Cell(3, 1, 1, amount)
            };
        }

        private static IReadOnlyList<DocumentElement> FourValueRows(params string[] subjects)
        {
            var elements = new List<DocumentElement>
            {
                Cell(0, 0, 0, AuditedHeader),
                Cell(1, 0, 1, "不含税评估原值"),
                Cell(2, 0, 2, "不含税评估净值"),
                Cell(3, 0, 3, "含税评估原值"),
                Cell(4, 0, 4, "含税评估净值")
            };
            int order = elements.Count;
            for (int index = 0; index < subjects.Length; index++)
            {
                int row = index + 1;
                elements.Add(Cell(order++, row, 0, subjects[index]));
                elements.Add(Cell(order++, row, 1, "10万元"));
                elements.Add(Cell(order++, row, 2, "9万元"));
                elements.Add(Cell(order++, row, 3, "11.3万元"));
                elements.Add(Cell(order++, row, 4, "10.17万元"));
            }

            return elements;
        }

        private static DocumentElement Cell(int order, int row, int cell, string text)
        {
            return new DocumentElement(
                DocumentElementType.TableCell,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    null,
                    0,
                    row,
                    cell,
                    new string[0]),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
