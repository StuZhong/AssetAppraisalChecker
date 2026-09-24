using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BusinessFields
{
    [TestClass]
    public sealed class TableBusinessExtractionTests
    {
        private readonly MoneyOccurrenceExtractor _extractor = new MoneyOccurrenceExtractor();

        [TestMethod]
        public void Extract_ExplicitTableAmounts_UsesColumnHeaders()
        {
            var elements = new List<DocumentElement>
            {
                Cell(0, 0, 0, 0, "序号"),
                Cell(1, 0, 0, 1, "资产名称"),
                Cell(2, 0, 0, 2, "评估原值"),
                Cell(3, 0, 0, 3, "评估净值"),
                Cell(4, 0, 1, 0, "1"),
                Cell(5, 0, 1, 1, "测试设备A"),
                Cell(6, 0, 1, 2, "180.00万元"),
                Cell(7, 0, 1, 3, "170.00万元")
            };

            var occurrences = _extractor.Extract(elements);

            Assert.AreEqual(2, occurrences.Count);
            Assert.AreEqual(ValueCategory.AppraisedOriginalValue, occurrences[0].FieldIdentity.ValueCategory);
            Assert.AreEqual(ValueCategory.AppraisedNetValue, occurrences[1].FieldIdentity.ValueCategory);
            Assert.AreEqual(ClassificationConfidence.High, occurrences[0].ClassificationConfidence);
            Assert.IsTrue(occurrences[0].ClassificationEvidence.Any(item => item.Contains("当前表格列标题")));
        }

        [TestMethod]
        public void Extract_BareTableAmounts_InheritsScopedUnitOnlyForMoneyColumns()
        {
            List<DocumentElement> elements = CreateUnitTable(0, 0, "38.58", "38.19");

            var occurrences = _extractor.Extract(elements);

            Assert.AreEqual(2, occurrences.Count);
            CollectionAssert.AreEqual(
                new[] { "38.58", "38.19" },
                occurrences.Select(item => item.RawText).ToArray());
            CollectionAssert.AreEqual(
                new[] { 385800m, 381900m },
                occurrences.Select(item => item.NormalizedAmountYuan).ToArray());
            Assert.IsFalse(occurrences.Any(item => item.RawText == "1" || item.RawText == "10"));
            Assert.IsTrue(occurrences.All(item => item.ClassificationEvidence.Any(
                evidence => evidence.Contains("当前表格存在明确金额单位“万元”"))));
        }

        [TestMethod]
        public void Extract_BareTableAmount_PreservesCellRelativeStartAndLength()
        {
            List<DocumentElement> elements = CreateUnitTable(0, 0, " 38.58 ", " 38.19 ");

            var occurrences = _extractor.Extract(elements);

            Assert.AreEqual(1, occurrences[0].TextStartIndex);
            Assert.AreEqual(5, occurrences[0].TextLength);
            Assert.AreEqual("38.58", occurrences[0].RawText);
        }

        [TestMethod]
        public void Extract_TableUnit_DoesNotLeakIntoFollowingTable()
        {
            List<DocumentElement> elements = CreateUnitTable(0, 0, "38.58", "38.19");
            int nextOrder = elements.Max(element => element.DocumentOrderIndex) + 1;
            elements.Add(Cell(nextOrder++, 1, 0, 0, "评估净值"));
            elements.Add(Cell(nextOrder, 1, 1, 0, "99.99"));

            var occurrences = _extractor.Extract(elements);

            Assert.AreEqual(2, occurrences.Count);
            Assert.IsFalse(occurrences.Any(item => item.RawText == "99.99"));
        }

        [TestMethod]
        public void Extract_ConflictingTableUnitDeclarations_DoesNotInherit()
        {
            var elements = new List<DocumentElement>
            {
                Paragraph(0, "金额单位：万元"),
                Cell(1, 0, 0, 0, "金额单位：元"),
                Cell(2, 0, 0, 1, "评估净值"),
                Cell(3, 0, 1, 1, "38.19")
            };

            Assert.AreEqual(0, _extractor.Extract(elements).Count);
        }

        [TestMethod]
        public void Extract_RowLabelAndUnit_CanClassifyPairedValueLayout()
        {
            var elements = new List<DocumentElement>
            {
                Paragraph(0, "金额单位：万元"),
                Cell(1, 0, 0, 0, "评估净值", new[] { "评估结果汇总表" }),
                Cell(2, 0, 0, 1, "170.00", new[] { "评估结果汇总表" })
            };

            MoneyOccurrence occurrence = _extractor.Extract(elements).Single();

            Assert.AreEqual(1700000m, occurrence.NormalizedAmountYuan);
            Assert.AreEqual(ValueCategory.AppraisedNetValue, occurrence.FieldIdentity.ValueCategory);
            Assert.AreEqual(SectionCategory.ResultTable, occurrence.FieldIdentity.SectionCategory);
            Assert.IsTrue(occurrence.ClassificationEvidence.Any(item => item.Contains("当前表格行标签")));
        }

        [TestMethod]
        public void Extract_ProjectRowsAndTotal_RowScopeDoesNotLeakAndTotalAppliesToEveryAmount()
        {
            var elements = new List<DocumentElement>
            {
                Paragraph(0, "金额单位：万元"),
                Cell(1, 0, 0, 0, "项目名称"),
                Cell(2, 0, 0, 1, "评估原值"),
                Cell(3, 0, 0, 2, "评估净值"),
                Cell(4, 0, 1, 0, "项目A"),
                Cell(5, 0, 1, 1, "100"),
                Cell(6, 0, 1, 2, "90"),
                Cell(7, 0, 2, 0, "项目B"),
                Cell(8, 0, 2, 1, "200"),
                Cell(9, 0, 2, 2, "180"),
                Cell(10, 0, 3, 0, "合计"),
                Cell(11, 0, 3, 1, "300"),
                Cell(12, 0, 3, 2, "270")
            };

            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(elements);
            IReadOnlyList<MoneyOccurrence> projectRows = occurrences
                .Where(item => item.Location.RowIndex == 1 || item.Location.RowIndex == 2)
                .ToList();
            IReadOnlyList<MoneyOccurrence> totalRow = occurrences
                .Where(item => item.Location.RowIndex == 3)
                .ToList();

            Assert.AreEqual(6, occurrences.Count);
            Assert.IsTrue(projectRows.All(item => item.FieldIdentity.Scope == Scope.Unspecified));
            Assert.IsTrue(totalRow.All(item => item.FieldIdentity.Scope == Scope.Subtotal));
            Assert.IsTrue(totalRow.All(item => item.SubjectKind == SubjectKind.Project));
            Assert.IsFalse(totalRow.Any(item => item.SubjectId == "合计"));
            Assert.IsTrue(projectRows.Where(item => item.Location.RowIndex == 1)
                .All(item => item.SubjectId == "项目A"));
            Assert.IsTrue(projectRows.Where(item => item.Location.RowIndex == 2)
                .All(item => item.SubjectId == "项目B"));
        }

        [TestMethod]
        public void Extract_CurrentRowScope_PrecedesPreviousContext()
        {
            var elements = new List<DocumentElement>
            {
                Paragraph(0, "项目合计评估净值100万元"),
                Cell(1, 0, 0, 0, "项目名称"),
                Cell(2, 0, 0, 1, "评估净值"),
                Cell(3, 0, 1, 0, "合计"),
                Cell(4, 0, 1, 1, "100万元", previousText: "项目合计")
            };

            MoneyOccurrence occurrence = _extractor.Extract(elements)
                .Single(item => item.Location.TableIndex.HasValue);

            Assert.AreEqual(Scope.Subtotal, occurrence.FieldIdentity.Scope);
            Assert.AreEqual(ClassificationConfidence.High, occurrence.ScopeConfidence);
        }

        [TestMethod]
        public void Extract_ExplicitProjectTotalRow_AppliesProjectTotalToEveryAmount()
        {
            var elements = new List<DocumentElement>
            {
                Cell(0, 0, 0, 0, "项目名称"),
                Cell(1, 0, 0, 1, "评估原值"),
                Cell(2, 0, 0, 2, "评估净值"),
                Cell(3, 0, 1, 0, "项目合计"),
                Cell(4, 0, 1, 1, "300万元"),
                Cell(5, 0, 1, 2, "270万元")
            };

            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(elements);

            Assert.AreEqual(2, occurrences.Count);
            Assert.IsTrue(occurrences.All(item => item.FieldIdentity.Scope == Scope.ProjectTotal));
            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Project));
        }

        private static List<DocumentElement> CreateUnitTable(
            int tableIndex,
            int initialOrder,
            string originalValue,
            string netValue)
        {
            return new List<DocumentElement>
            {
                Paragraph(initialOrder, "金额单位：万元"),
                Cell(initialOrder + 1, tableIndex, 0, 0, "序号"),
                Cell(initialOrder + 2, tableIndex, 0, 1, "资产名称"),
                Cell(initialOrder + 3, tableIndex, 0, 2, "数量"),
                Cell(initialOrder + 4, tableIndex, 0, 3, "评估原值"),
                Cell(initialOrder + 5, tableIndex, 0, 4, "评估净值"),
                Cell(initialOrder + 6, tableIndex, 1, 0, "1"),
                Cell(initialOrder + 7, tableIndex, 1, 1, "测试设备A"),
                Cell(initialOrder + 8, tableIndex, 1, 2, "10"),
                Cell(initialOrder + 9, tableIndex, 1, 3, originalValue),
                Cell(initialOrder + 10, tableIndex, 1, 4, netValue)
            };
        }

        private static DocumentElement Paragraph(int order, string text)
        {
            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(DocumentPart.MainBody, order, null, null, null, new string[0]),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }

        private static DocumentElement Cell(
            int order,
            int tableIndex,
            int rowIndex,
            int cellIndex,
            string text,
            IEnumerable<string> headingPath = null,
            string previousText = null)
        {
            return new DocumentElement(
                DocumentElementType.TableCell,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    null,
                    tableIndex,
                    rowIndex,
                    cellIndex,
                    headingPath ?? new string[0]),
                order,
                string.Empty,
                new DocumentNearbyContext(previousText ?? string.Empty, text, string.Empty));
        }
    }
}
