using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class QualifiedProjectNameSubjectTests
    {
        private readonly MoneyOccurrenceExtractor _extractor = new MoneyOccurrenceExtractor();

        [TestMethod]
        [DataRow("项目名称")]
        [DataRow("工程项目名称")]
        [DataRow("资产项目名称")]
        [DataRow("费用项目名称")]
        [DataRow("评估项目名称")]
        public void Extract_StructuredProjectNameColumn_AssignsStableDetailSubjects(string header)
        {
            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(
                CreateDetailTable(
                    0,
                    0,
                    header,
                    new[] { "测试工程A", "测试工程B" },
                    new[] { "90万元", "180万元" },
                    "报告摘要"));

            Assert.AreEqual(2, occurrences.Count);
            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Asset));
            CollectionAssert.AreEqual(
                new[] { "测试工程A", "测试工程B" },
                occurrences.Select(item => item.SubjectId).ToArray());
            Assert.IsTrue(occurrences.All(item => item.SubjectConfidence == ClassificationConfidence.High));
            Assert.IsTrue(occurrences.All(item => item.SubjectEvidence.Any(
                evidence => evidence.Contains("项目名称") && evidence.Contains("多行明细"))));
        }

        [TestMethod]
        public void Extract_ProjectNameColumnWithoutRecognizedMoneyColumn_RemainsUnknown()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(
                CreateDetailTable(
                    0,
                    0,
                    "工程项目名称",
                    new[] { "测试工程A", "测试工程B" },
                    new[] { "90万元", "180万元" },
                    "报告摘要",
                    "金额"));

            Assert.AreEqual(2, occurrences.Count);
            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_ProjectNameColumnWithOnlyOneDetailRow_RemainsUnknown()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(
                CreateDetailTable(
                    0,
                    0,
                    "评估项目名称",
                    new[] { "测试工程A" },
                    new[] { "90万元" },
                    "报告摘要"));

            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual(SubjectKind.Unknown, occurrences[0].SubjectKind);
        }

        [TestMethod]
        public void Extract_SingleRowProjectMetadataTable_DoesNotCreateMoneySubject()
        {
            var elements = new[]
            {
                Cell(0, 0, 0, 0, "评估项目名称", "报告摘要"),
                Cell(1, 0, 0, 1, "XX测试资产评估项目", "报告摘要")
            };

            Assert.AreEqual(0, _extractor.Extract(elements).Count);
        }

        [TestMethod]
        [DataRow("1001", "1002")]
        [DataRow("10万元", "20万元")]
        [DataRow("10台", "20项")]
        public void Extract_InvalidProjectDetailValues_DoNotEstablishSubjectColumn(
            string firstSubject,
            string secondSubject)
        {
            IReadOnlyList<MoneyOccurrence> amountColumnOccurrences = _extractor.Extract(
                    CreateDetailTable(
                        0,
                        0,
                        "费用项目名称",
                        new[] { firstSubject, secondSubject },
                        new[] { "90万元", "180万元" },
                        "报告摘要"))
                .Where(item => item.Location.CellIndex == 1)
                .ToList();

            Assert.AreEqual(2, amountColumnOccurrences.Count);
            Assert.IsTrue(amountColumnOccurrences.All(
                item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        [DataRow("合计", Scope.Subtotal)]
        [DataRow("总计", Scope.ProjectTotal)]
        [DataRow("项目合计", Scope.ProjectTotal)]
        [DataRow("评估结果合计", Scope.ProjectTotal)]
        [DataRow("评估总值", Scope.ProjectTotal)]
        [DataRow("评估结果汇总", Scope.ProjectTotal)]
        public void Extract_AggregateRowNeverBecomesDetailSubject(
            string aggregateLabel,
            Scope expectedScope)
        {
            List<DocumentElement> elements = CreateDetailTable(
                0,
                0,
                "工程项目名称",
                new[] { "测试工程A", "测试工程B", aggregateLabel },
                new[] { "90万元", "180万元", "270万元" },
                "评估结论");

            MoneyOccurrence aggregate = _extractor.Extract(elements)
                .Single(item => item.Location.RowIndex == 3 && item.Location.CellIndex == 1);

            Assert.AreEqual(expectedScope, aggregate.FieldIdentity.Scope);
            Assert.AreNotEqual(SubjectKind.Asset, aggregate.SubjectKind);
            Assert.AreNotEqual(aggregateLabel, aggregate.SubjectId);
        }

        [TestMethod]
        public void Check_DifferentQualifiedProjectRows_RemainSeparated()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(
                CreateDetailTable(
                    0,
                    0,
                    "资产项目名称",
                    new[] { "项目A", "项目B" },
                    new[] { "90万元", "180万元" },
                    "报告摘要"));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreNotEqual(occurrences[0].ComparisonSubjectKey, occurrences[1].ComparisonSubjectKey);
        }

        [TestMethod]
        public void Check_SameQualifiedProjectRowsAcrossTablesAndSections_AreConsistent()
        {
            List<DocumentElement> elements = CreateDetailTable(
                0,
                0,
                "工程项目名称",
                new[] { "项目A", "项目B" },
                new[] { "90万元", "180万元" },
                "报告摘要");
            elements.AddRange(CreateDetailTable(
                1,
                elements.Count,
                "工程项目名称",
                new[] { "项目A", "项目B" },
                new[] { "90万元", "180万元" },
                "评估结论"));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(
                _extractor.Extract(elements));

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.IsTrue(result.ComparisonGroups.All(group => group.Occurrences.Count == 2));
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_SameQualifiedProjectRowWithDifferentAmount_ProducesConflict()
        {
            List<DocumentElement> elements = CreateDetailTable(
                0,
                0,
                "费用项目名称",
                new[] { "项目A", "项目B" },
                new[] { "90万元", "180万元" },
                "报告摘要");
            elements.AddRange(CreateDetailTable(
                1,
                elements.Count,
                "费用项目名称",
                new[] { "项目A", "项目B" },
                new[] { "80万元", "180万元" },
                "评估结论"));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(
                _extractor.Extract(elements));

            Assert.AreEqual(2, result.ComparisonGroups.Count);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueType.MoneyConflict, result.Issues[0].IssueType);
            Assert.AreEqual(100000m, result.Issues[0].Difference);
        }

        [TestMethod]
        public void Check_FourQualifiedDetailSubjectsRecoverFourSafeGroups()
        {
            string[] subjects = { "项目A", "项目B", "项目C", "项目D" };
            string[] amounts = { "90万元", "180万元", "270万元", "360万元" };
            List<DocumentElement> elements = CreateDetailTable(
                0,
                0,
                "评估项目名称",
                subjects,
                amounts,
                "报告摘要");
            elements.AddRange(CreateDetailTable(
                1,
                elements.Count,
                "评估项目名称",
                subjects,
                amounts,
                "评估结论"));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(
                _extractor.Extract(elements));

            Assert.AreEqual(4, result.ComparisonGroups.Count);
            Assert.IsTrue(result.ComparisonGroups.All(group => group.Occurrences.Count == 2));
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(0, result.SkippedOccurrenceCount);
            Assert.AreEqual(
                4,
                result.ComparisonGroups.Select(group =>
                    group.ComparisonKey.ComparisonSubjectKey).Distinct().Count());
        }

        [TestMethod]
        public void Extract_QualifiedProjectDetailSubject_UsesExistingSafeNormalization()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(
                CreateDetailTable(
                    0,
                    0,
                    "工程项目名称",
                    new[] { "  测试　项目A  ", "测试项目B" },
                    new[] { "90万元", "180万元" },
                    "报告摘要"));

            Assert.AreEqual("测试 项目A", occurrences[0].SubjectId);
            Assert.AreEqual("测试项目B", occurrences[1].SubjectId);
        }

        private static List<DocumentElement> CreateDetailTable(
            int tableIndex,
            int initialOrder,
            string subjectHeader,
            IReadOnlyList<string> subjects,
            IReadOnlyList<string> amounts,
            string heading,
            string amountHeader = "不含税评估净值")
        {
            Assert.AreEqual(subjects.Count, amounts.Count);
            var elements = new List<DocumentElement>
            {
                Cell(initialOrder, tableIndex, 0, 0, subjectHeader, heading),
                Cell(initialOrder + 1, tableIndex, 0, 1, amountHeader, heading)
            };
            int order = initialOrder + 2;
            for (int index = 0; index < subjects.Count; index++)
            {
                elements.Add(Cell(order++, tableIndex, index + 1, 0, subjects[index], heading));
                elements.Add(Cell(order++, tableIndex, index + 1, 1, amounts[index], heading));
            }

            return elements;
        }

        private static DocumentElement Cell(
            int order,
            int table,
            int row,
            int cell,
            string text,
            string heading)
        {
            return new DocumentElement(
                DocumentElementType.TableCell,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    null,
                    table,
                    row,
                    cell,
                    new[] { heading }),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
