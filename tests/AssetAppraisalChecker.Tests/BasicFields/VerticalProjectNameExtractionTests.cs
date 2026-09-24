using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class VerticalProjectNameExtractionTests
    {
        private readonly BasicFieldExtractor _extractor = new BasicFieldExtractor();

        [TestMethod]
        public void Extract_ExactProjectNameHeader_ReturnsVerticalValue()
        {
            BasicFieldOccurrence occurrence = ExtractSingleProjectName(
                Table(0, 0, "项目名称", new[] { "测试项目甲" }));

            Assert.AreEqual("测试项目甲", occurrence.RawValue);
        }

        [TestMethod]
        public void Extract_AppraisalProjectNameHeader_ReturnsVerticalValue()
        {
            BasicFieldOccurrence occurrence = ExtractSingleProjectName(
                Table(0, 0, "评估项目名称", new[] { "测试项目甲" }));

            Assert.AreEqual("测试项目甲", occurrence.NormalizedValue.CanonicalValue);
        }

        [TestMethod]
        public void Extract_AnotherQualifiedProjectNameHeader_ReturnsVerticalValue()
        {
            BasicFieldOccurrence occurrence = ExtractSingleProjectName(
                Table(0, 0, "本次评估项目名称", new[] { "测试项目甲" }));

            Assert.AreEqual(BasicFieldType.ProjectName, occurrence.FieldType);
        }

        [TestMethod]
        public void Extract_SingleProjectValueWithoutAggregate_IsConfirmedByBusinessColumn()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                Table(0, 0, "某评估项目名称", new[] { "测试项目甲" }, false));

            Assert.AreEqual(1, occurrences.Count(item => item.FieldType == BasicFieldType.ProjectName));
        }

        [TestMethod]
        public void Check_TwoTablesWithSameProjectName_ReturnNoConflict()
        {
            List<DocumentElement> elements = Table(
                0, 0, "某评估项目名称", new[] { "测试项目甲" });
            elements.AddRange(Table(
                1, elements.Count, "另一评估项目名称", new[] { "测试项目甲" }));

            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(elements);
            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, occurrences.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_TwoTablesWithDifferentProjectNames_ReturnSeriousConflict()
        {
            List<DocumentElement> elements = Table(
                0, 0, "某评估项目名称", new[] { "测试项目甲" });
            elements.AddRange(Table(
                1, elements.Count, "另一评估项目名称", new[] { "测试项目乙" }));

            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(ProjectNames(elements));

            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueType.BasicFieldConflict, result.Issues[0].IssueType);
            Assert.AreEqual(IssueSeverity.Serious, result.Issues[0].Severity);
        }

        [TestMethod]
        public void Extract_AggregateRowNeverBecomesProjectName()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(
                Table(0, 0, "某评估项目名称", new[] { "测试项目甲" }));

            Assert.AreEqual(1, occurrences.Count);
            Assert.IsFalse(occurrences.Any(item => item.RawValue == "合计"));
        }

        [TestMethod]
        public void Extract_MultipleProjectDetailRows_FailsClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(Table(
                0,
                0,
                "项目名称",
                new[] { "设备A", "设备B", "设备C" }));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_QualifiedExpenseProjectList_FailsClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(Table(
                0,
                0,
                "费用项目名称",
                new[] { "设计费", "建设费", "其他费" },
                true,
                "评估价值"));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_PureNumberValue_IsRejected()
        {
            AssertNoProjectName("12345");
        }

        [TestMethod]
        public void Extract_MoneyValue_IsRejected()
        {
            AssertNoProjectName("100万元");
        }

        [TestMethod]
        public void Extract_DateValue_IsRejected()
        {
            AssertNoProjectName("2026年6月30日");
        }

        [TestMethod]
        public void Extract_PercentageValue_IsRejected()
        {
            AssertNoProjectName("13%");
        }

        [TestMethod]
        public void Extract_QuantityValue_IsRejected()
        {
            AssertNoProjectName("10台");
        }

        [TestMethod]
        public void Extract_GenericPlaceholderValue_IsRejected()
        {
            AssertNoProjectName("待定");
        }

        [TestMethod]
        public void Extract_HorizontalLabelValueTable_DoesNotCreateDuplicate()
        {
            List<DocumentElement> elements = RowOnlyTable(
                0,
                0,
                "项目名称",
                "测试项目甲");

            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(elements);

            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual("测试项目甲", occurrences[0].RawValue);
        }

        [TestMethod]
        public void Extract_HorizontalProjectValueContainingBusinessWords_RemainsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(RowOnlyTable(
                0,
                0,
                "项目名称",
                "测试资产评估价值咨询项目"));

            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual("测试资产评估价值咨询项目", occurrences[0].RawValue);
        }

        [TestMethod]
        public void Extract_VerticalValue_PreservesValueCellLocationAndTextRange()
        {
            BasicFieldOccurrence occurrence = ExtractSingleProjectName(
                Table(3, 0, "某评估项目名称", new[] { "测试项目甲" }));

            Assert.AreEqual(3, occurrence.Location.TableIndex);
            Assert.AreEqual(1, occurrence.Location.RowIndex);
            Assert.AreEqual(0, occurrence.Location.CellIndex);
            CollectionAssert.AreEqual(
                new[] { "评估结论" },
                occurrence.Location.HeadingPath.ToArray());
            Assert.AreEqual("测试项目甲", occurrence.RawText);
            Assert.AreEqual("测试项目甲", occurrence.RawValue);
            Assert.AreEqual(0, occurrence.TextStartIndex);
            Assert.AreEqual("测试项目甲".Length, occurrence.TextLength);
        }

        [TestMethod]
        public void Extract_StandaloneReportNumber_RemainsUnchanged()
        {
            List<DocumentElement> elements = Table(
                0, 1, "某评估项目名称", new[] { "测试项目甲" });
            elements.Insert(0, Paragraph(0, "云测试评报字〔2026〕第001号"));

            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(elements);

            Assert.AreEqual(1, occurrences.Count(item => item.FieldType == BasicFieldType.ReportNumber));
            Assert.AreEqual(
                "云测试评报字〔2026〕第001号",
                occurrences.Single(item => item.FieldType == BasicFieldType.ReportNumber)
                    .NormalizedValue.CanonicalValue);
        }

        [TestMethod]
        public void Extract_ValuationDate_RemainsUnchanged()
        {
            var elements = new List<DocumentElement>
            {
                Paragraph(0, "评估基准日：2026年6月30日")
            };
            elements.AddRange(Table(
                0, elements.Count, "某评估项目名称", new[] { "测试项目甲" }));

            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(elements);

            Assert.AreEqual(1, occurrences.Count(item =>
                item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_ProjectName_DoesNotChangeMoneySubjectResolution()
        {
            List<DocumentElement> elements = Table(
                0, 0, "某评估项目名称", new[] { "测试项目甲" });
            elements.AddRange(Table(
                1, elements.Count, "另一评估项目名称", new[] { "测试项目甲" }));
            var moneyExtractor = new MoneyOccurrenceExtractor();
            IReadOnlyList<MoneyOccurrence> before = moneyExtractor.Extract(elements);

            Assert.AreEqual(2, ProjectNames(elements).Count);
            IReadOnlyList<MoneyOccurrence> after = moneyExtractor.Extract(elements);

            Assert.AreEqual(before.Count, after.Count);
            Assert.IsTrue(after
                .Where(item => item.Location.RowIndex == 1)
                .All(item => item.SubjectKind == SubjectKind.Asset));
            CollectionAssert.AreEqual(
                before.Select(MoneySignature).ToArray(),
                after.Select(MoneySignature).ToArray());
        }

        [TestMethod]
        public void Extract_ProjectName_DoesNotChangeMoneyComparisonResult()
        {
            List<DocumentElement> elements = Table(
                0, 0, "某评估项目名称", new[] { "测试项目甲" });
            elements.AddRange(Table(
                1, elements.Count, "另一评估项目名称", new[] { "测试项目甲" }));
            var moneyExtractor = new MoneyOccurrenceExtractor();
            var consistencyEngine = new ConsistencyEngine();
            ConsistencyCheckResult before = consistencyEngine.Check(
                moneyExtractor.Extract(elements));

            Assert.AreEqual(2, ProjectNames(elements).Count);
            ConsistencyCheckResult after = consistencyEngine.Check(
                moneyExtractor.Extract(elements));

            Assert.AreEqual(before.ComparisonGroups.Count, after.ComparisonGroups.Count);
            Assert.AreEqual(before.Issues.Count, after.Issues.Count);
            Assert.AreEqual(before.SkippedOccurrenceCount, after.SkippedOccurrenceCount);
        }

        [TestMethod]
        public void Extract_MissingBusinessValueHeader_FailsClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(Table(
                0, 0, "某评估项目名称", new[] { "测试项目甲" }, true, "金额"));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_UnpopulatedBusinessValueCell_FailsClosed()
        {
            List<DocumentElement> elements = Table(
                0, 0, "某评估项目名称", new[] { "测试项目甲" });
            DocumentElement amountCell = elements.Single(item =>
                item.Location.RowIndex == 1 && item.Location.CellIndex == 1);
            int index = elements.IndexOf(amountCell);
            elements[index] = Cell(
                amountCell.DocumentOrderIndex,
                0,
                1,
                1,
                string.Empty);

            Assert.AreEqual(0, ProjectNames(elements).Count);
        }

        [TestMethod]
        public void Extract_RepeatedSameProjectValueRows_FailsClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(Table(
                0,
                0,
                "某评估项目名称",
                new[] { "测试项目甲", "测试项目甲" }));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_AggregateLabelOutsideProjectColumn_IsIgnoredAsWholeRow()
        {
            var elements = new List<DocumentElement>
            {
                Cell(0, 0, 0, 0, "某评估项目名称"),
                Cell(1, 0, 0, 1, "说明"),
                Cell(2, 0, 0, 2, "评估净值"),
                Cell(3, 0, 1, 0, "测试项目甲"),
                Cell(4, 0, 1, 1, "明细"),
                Cell(5, 0, 1, 2, "100万元"),
                Cell(6, 0, 2, 0, string.Empty),
                Cell(7, 0, 2, 1, "评估结果合计"),
                Cell(8, 0, 2, 2, "100万元")
            };

            BasicFieldOccurrence occurrence = ExtractSingleProjectName(elements);

            Assert.AreEqual("测试项目甲", occurrence.RawValue);
            Assert.IsTrue(occurrence.Evidence.Any(text => text.Contains("非聚合数据区域")));
        }

        private void AssertNoProjectName(string value)
        {
            Assert.AreEqual(
                0,
                ProjectNames(Table(
                    0, 0, "某评估项目名称", new[] { value })).Count,
                value);
        }

        private BasicFieldOccurrence ExtractSingleProjectName(
            IEnumerable<DocumentElement> elements)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(elements);
            Assert.AreEqual(1, occurrences.Count);
            return occurrences[0];
        }

        private IReadOnlyList<BasicFieldOccurrence> ProjectNames(
            IEnumerable<DocumentElement> elements)
        {
            return _extractor.Extract(elements)
                .Where(item => item.FieldType == BasicFieldType.ProjectName)
                .ToList();
        }

        private static List<DocumentElement> Table(
            int tableIndex,
            int initialOrder,
            string projectHeader,
            IReadOnlyList<string> projectValues,
            bool includeAggregate = true,
            string moneyHeader = "评估净值")
        {
            var elements = new List<DocumentElement>
            {
                Cell(initialOrder, tableIndex, 0, 0, projectHeader),
                Cell(initialOrder + 1, tableIndex, 0, 1, moneyHeader)
            };
            int order = initialOrder + 2;
            for (int index = 0; index < projectValues.Count; index++)
            {
                elements.Add(Cell(order++, tableIndex, index + 1, 0, projectValues[index]));
                elements.Add(Cell(
                    order++,
                    tableIndex,
                    index + 1,
                    1,
                    (100 + index * 10) + "万元"));
            }

            if (includeAggregate)
            {
                int aggregateRow = projectValues.Count + 1;
                elements.Add(Cell(order++, tableIndex, aggregateRow, 0, "合计"));
                elements.Add(Cell(order, tableIndex, aggregateRow, 1, "100万元"));
            }

            return elements;
        }

        private static List<DocumentElement> RowOnlyTable(
            int tableIndex,
            int initialOrder,
            string label,
            string value)
        {
            return new List<DocumentElement>
            {
                Cell(initialOrder, tableIndex, 0, 0, label),
                Cell(initialOrder + 1, tableIndex, 0, 1, value)
            };
        }

        private static DocumentElement Paragraph(int order, string text)
        {
            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    order,
                    null,
                    null,
                    null,
                    new[] { "报告摘要" }),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }

        private static DocumentElement Cell(
            int order,
            int table,
            int row,
            int cell,
            string text)
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
                    new[] { "评估结论" }),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }

        private static string MoneySignature(MoneyOccurrence occurrence)
        {
            return string.Join(
                "|",
                occurrence.Location.TableIndex,
                occurrence.Location.RowIndex,
                occurrence.Location.CellIndex,
                occurrence.SubjectKind,
                occurrence.SubjectId,
                occurrence.NormalizedAmountYuan);
        }
    }
}
