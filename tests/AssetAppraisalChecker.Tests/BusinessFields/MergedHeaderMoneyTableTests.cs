using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BusinessFields
{
    [TestClass]
    public sealed class MergedHeaderMoneyTableTests
    {
        private readonly MoneyOccurrenceExtractor _extractor = new MoneyOccurrenceExtractor();

        [TestMethod]
        public void Extract_AuditedDoubleHeader_ResolvesDetailAndProjectTotalSemantics()
        {
            using (SyntheticDocxFile file = CreateAuditedMergedHeaderDocument())
            {
                DocumentReadResult document = new DocxDocumentReader().Read(file.FilePath);
                IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(document);
                IReadOnlyList<MoneyOccurrence> tableOccurrences = occurrences
                    .Where(item => item.Location.TableIndex == 0)
                    .ToList();

                Assert.IsTrue(document.IsSuccess);
                Assert.AreEqual(12, tableOccurrences.Count);
                Assert.AreEqual(3, Count(tableOccurrences, ValueCategory.AppraisedOriginalValue, TaxBasis.TaxExcluded));
                Assert.AreEqual(3, Count(tableOccurrences, ValueCategory.AppraisedNetValue, TaxBasis.TaxExcluded));
                Assert.AreEqual(3, Count(tableOccurrences, ValueCategory.AppraisedOriginalValue, TaxBasis.TaxIncluded));
                Assert.AreEqual(3, Count(tableOccurrences, ValueCategory.AppraisedNetValue, TaxBasis.TaxIncluded));

                IReadOnlyList<MoneyOccurrence> total = tableOccurrences
                    .Where(item => item.Location.RowIndex == 4)
                    .ToList();
                Assert.AreEqual(4, total.Count);
                Assert.IsTrue(
                    total.All(item => item.FieldIdentity.Scope == Scope.ProjectTotal),
                    string.Join(",", total.Select(item =>
                        item.FieldIdentity.Scope + "/" + item.SubjectKind)));
                Assert.IsTrue(total.All(item => item.SubjectKind == SubjectKind.Project));

                ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);
                Assert.IsTrue(result.ComparisonGroups.Any(group =>
                    group.ComparisonKey.ValueCategory == ValueCategory.AppraisedNetValue &&
                    group.ComparisonKey.TaxBasis == TaxBasis.TaxExcluded &&
                    group.ComparisonKey.Scope == Scope.ProjectTotal &&
                    group.Occurrences.Count == 2 &&
                    group.Occurrences.Any(item => item.Location.TableIndex == 0) &&
                    group.Occurrences.Any(item => !item.Location.TableIndex.HasValue)));
                Assert.IsTrue(result.ComparisonGroups.Any(group =>
                    group.ComparisonKey.ValueCategory == ValueCategory.AppraisedNetValue &&
                    group.ComparisonKey.TaxBasis == TaxBasis.TaxIncluded &&
                    group.ComparisonKey.Scope == Scope.ProjectTotal &&
                    group.Occurrences.Count == 2 &&
                    group.Occurrences.Any(item => item.Location.TableIndex == 0) &&
                    group.Occurrences.Any(item => !item.Location.TableIndex.HasValue)));

                IReadOnlyList<MoneyOccurrence> detailNetExcluded = tableOccurrences
                    .Where(item =>
                        item.Location.RowIndex == 2 || item.Location.RowIndex == 3)
                    .Where(item =>
                        item.FieldIdentity.ValueCategory == ValueCategory.AppraisedNetValue &&
                        item.FieldIdentity.TaxBasis == TaxBasis.TaxExcluded)
                    .ToList();
                Assert.AreEqual(2, detailNetExcluded.Count);
                Assert.AreEqual(2, detailNetExcluded.Select(item => item.SubjectId).Distinct().Count());
                Assert.IsFalse(result.ComparisonGroups.Any(group =>
                    group.ComparisonKey.ValueCategory == ValueCategory.AppraisedNetValue &&
                    group.ComparisonKey.TaxBasis == TaxBasis.TaxExcluded &&
                    group.ComparisonKey.ComparisonSubjectKey.SubjectKind == SubjectKind.Asset &&
                    group.Occurrences.Count > 1));
            }
        }

        [TestMethod]
        public void Read_GridSpan_RecordsLogicalCoordinatesIndependentOfPhysicalCellIndex()
        {
            using (SyntheticDocxFile file = CreateAuditedMergedHeaderDocument())
            {
                DocumentReadResult document = new DocxDocumentReader().Read(file.FilePath);
                DocumentElement includedParent = document.Elements.Single(item =>
                    item.Location.TableIndex == 0 &&
                    item.Location.RowIndex == 0 &&
                    item.Location.CellIndex == 4);
                DocumentElement firstTotalAmount = document.Elements.Single(item =>
                    item.Location.TableIndex == 0 &&
                    item.Location.RowIndex == 4 &&
                    item.Location.CellIndex == 2);

                Assert.AreEqual(8, includedParent.Location.TableGridColumnCount);
                Assert.AreEqual(5, includedParent.Location.LogicalGridStart);
                Assert.AreEqual(2, includedParent.Location.LogicalGridSpan);
                Assert.AreEqual(6, includedParent.Location.LogicalGridEnd);
                Assert.AreEqual(3, firstTotalAmount.Location.LogicalGridStart);
                Assert.AreNotEqual(
                    firstTotalAmount.Location.CellIndex,
                    firstTotalAmount.Location.LogicalGridStart);
            }
        }

        [TestMethod]
        public void Extract_MergedChildrenWithoutParents_FailsClosed()
        {
            List<DocumentElement> elements = UnitParagraphAnd(
                Cell(1, 0, 0, 0, "原值", 4, 0, 1),
                Cell(2, 0, 0, 1, "净值", 4, 1, 1),
                Cell(3, 0, 0, 2, "原值", 4, 2, 1),
                Cell(4, 0, 0, 3, "净值", 4, 3, 1),
                Cell(5, 0, 1, 0, "10", 4, 0, 1),
                Cell(6, 0, 1, 1, "9", 4, 1, 1),
                Cell(7, 0, 1, 2, "11", 4, 2, 1),
                Cell(8, 0, 1, 3, "10", 4, 3, 1));

            Assert.AreEqual(0, _extractor.Extract(elements).Count);
        }

        [TestMethod]
        public void Extract_MergedParentsWithMissingChild_FailsClosed()
        {
            List<DocumentElement> elements = CompleteParentsWithChildren(
                "原值", string.Empty, "原值", "净值");

            Assert.AreEqual(0, _extractor.Extract(elements).Count);
        }

        [TestMethod]
        public void Extract_CompetingMergedParents_FailsClosed()
        {
            List<DocumentElement> elements = UnitParagraphAnd(
                Cell(1, 0, 0, 0, "评估价值（不含税）", 4, 0, 2),
                Cell(2, 0, 0, 1, "评估价值（含税）", 4, 2, 2),
                Cell(3, 0, 1, 0, "评估价值（不含税）", 4, 0, 2),
                Cell(4, 0, 1, 1, string.Empty, 4, 2, 2),
                Cell(5, 0, 2, 0, "原值", 4, 0, 1),
                Cell(6, 0, 2, 1, "净值", 4, 1, 1),
                Cell(7, 0, 2, 2, "原值", 4, 2, 1),
                Cell(8, 0, 2, 3, "净值", 4, 3, 1),
                Cell(9, 0, 3, 0, "10", 4, 0, 1),
                Cell(10, 0, 3, 1, "9", 4, 1, 1),
                Cell(11, 0, 3, 2, "11", 4, 2, 1),
                Cell(12, 0, 3, 3, "10", 4, 3, 1));

            Assert.AreEqual(0, _extractor.Extract(elements).Count);
        }

        [TestMethod]
        public void Extract_GenericMergedAppraisedValueWithoutChildren_DoesNotGuess()
        {
            List<DocumentElement> elements = UnitParagraphAnd(
                Cell(1, 0, 0, 0, "评估价值", 2, 0, 2),
                Cell(2, 0, 1, 0, "10", 2, 0, 1),
                Cell(3, 0, 1, 1, "9", 2, 1, 1));

            Assert.AreEqual(0, _extractor.Extract(elements).Count);
        }

        [TestMethod]
        public void Extract_NonMoneyMergedHeader_DoesNotActivateMoneyRule()
        {
            List<DocumentElement> elements = UnitParagraphAnd(
                Cell(1, 0, 0, 0, "设备说明", 2, 0, 2),
                Cell(2, 0, 1, 0, "10", 2, 0, 1),
                Cell(3, 0, 1, 1, "9", 2, 1, 1));

            Assert.AreEqual(0, _extractor.Extract(elements).Count);
        }

        [TestMethod]
        public void Extract_SingleLevelHeaders_PreserveExistingBehavior()
        {
            var elements = new List<DocumentElement>
            {
                Paragraph(0, "金额单位：万元"),
                LegacyCell(1, 0, 0, 0, "评估原值"),
                LegacyCell(2, 0, 0, 1, "评估净值"),
                LegacyCell(3, 0, 1, 0, "10"),
                LegacyCell(4, 0, 1, 1, "9")
            };

            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(elements);

            Assert.AreEqual(2, occurrences.Count);
            Assert.AreEqual(ValueCategory.AppraisedOriginalValue, occurrences[0].FieldIdentity.ValueCategory);
            Assert.AreEqual(ValueCategory.AppraisedNetValue, occurrences[1].FieldIdentity.ValueCategory);
        }

        [TestMethod]
        public void NoTableDocumentMoneyRegression()
        {
            var elements = new[]
            {
                Paragraph(0, "本项目不含税评估净值为100万元。")
            };

            MoneyOccurrence occurrence = _extractor.Extract(elements).Single();
            ConsistencyCheckResult result = new ConsistencyEngine().Check(new[] { occurrence });

            Assert.AreEqual(ValueCategory.AppraisedNetValue, occurrence.FieldIdentity.ValueCategory);
            Assert.AreEqual(TaxBasis.TaxExcluded, occurrence.FieldIdentity.TaxBasis);
            Assert.AreEqual(Scope.ProjectTotal, occurrence.FieldIdentity.Scope);
            Assert.AreEqual(SubjectKind.Project, occurrence.SubjectKind);
            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Extract_GridSpanOutsideDeclaredGrid_FailsClosed()
        {
            List<DocumentElement> elements = UnitParagraphAnd(
                Cell(1, 0, 0, 0, "评估价值（不含税）", 3, 0, 2),
                Cell(2, 0, 0, 1, "评估价值（含税）", 3, 2, 2),
                Cell(3, 0, 1, 0, "原值", 3, 0, 1),
                Cell(4, 0, 1, 1, "净值", 3, 1, 1),
                Cell(5, 0, 1, 2, "原值", 3, 2, 1),
                Cell(6, 0, 1, 3, "净值", 3, 3, 1),
                Cell(7, 0, 2, 0, "10", 3, 0, 1),
                Cell(8, 0, 2, 1, "9", 3, 1, 1),
                Cell(9, 0, 2, 2, "11", 3, 2, 1),
                Cell(10, 0, 2, 3, "10", 3, 3, 1));

            Assert.AreEqual(0, _extractor.Extract(elements).Count);
        }

        private static int Count(
            IEnumerable<MoneyOccurrence> occurrences,
            ValueCategory category,
            TaxBasis taxBasis)
        {
            return occurrences.Count(item =>
                item.FieldIdentity.ValueCategory == category &&
                item.FieldIdentity.TaxBasis == taxBasis);
        }

        private static SyntheticDocxFile CreateAuditedMergedHeaderDocument()
        {
            return SyntheticDocxFile.Create(
                ParagraphElement("本项目不含税评估净值为300万元。"),
                ParagraphElement("本项目含税评估净值为339万元。"),
                ParagraphElement("金额单位：万元"),
                new Table(
                    new TableGrid(
                        new GridColumn(), new GridColumn(), new GridColumn(), new GridColumn(),
                        new GridColumn(), new GridColumn(), new GridColumn(), new GridColumn()),
                    Row(
                        XmlCell("序号"),
                        XmlCell("设备名称"),
                        XmlCell("数量"),
                        XmlCell("评估价值（不含税）", 2),
                        XmlCell("评估价值（含税）", 2),
                        XmlCell("备注")),
                    Row(
                        XmlCell(string.Empty), XmlCell(string.Empty), XmlCell(string.Empty),
                        XmlCell("原值"), XmlCell("净值"),
                        XmlCell("原值"), XmlCell("净值"), XmlCell(string.Empty)),
                    Row(
                        XmlCell("1"), XmlCell("虚构设备甲"), XmlCell("1"),
                        XmlCell("100"), XmlCell("90"), XmlCell("113"), XmlCell("101.7"),
                        XmlCell(string.Empty)),
                    Row(
                        XmlCell("2"), XmlCell("虚构设备乙"), XmlCell("1"),
                        XmlCell("220"), XmlCell("210"), XmlCell("248.6"), XmlCell("237.3"),
                        XmlCell(string.Empty)),
                    Row(
                        XmlCell("合  计", 2), XmlCell(string.Empty),
                        XmlCell("320"), XmlCell("300"), XmlCell("361.6"), XmlCell("339"),
                        XmlCell(string.Empty))));
        }

        private static List<DocumentElement> CompleteParentsWithChildren(
            string child1,
            string child2,
            string child3,
            string child4)
        {
            return UnitParagraphAnd(
                Cell(1, 0, 0, 0, "评估价值（不含税）", 4, 0, 2),
                Cell(2, 0, 0, 1, "评估价值（含税）", 4, 2, 2),
                Cell(3, 0, 1, 0, child1, 4, 0, 1),
                Cell(4, 0, 1, 1, child2, 4, 1, 1),
                Cell(5, 0, 1, 2, child3, 4, 2, 1),
                Cell(6, 0, 1, 3, child4, 4, 3, 1),
                Cell(7, 0, 2, 0, "10", 4, 0, 1),
                Cell(8, 0, 2, 1, "9", 4, 1, 1),
                Cell(9, 0, 2, 2, "11", 4, 2, 1),
                Cell(10, 0, 2, 3, "10", 4, 3, 1));
        }

        private static List<DocumentElement> UnitParagraphAnd(params DocumentElement[] cells)
        {
            var result = new List<DocumentElement> { Paragraph(0, "金额单位：万元") };
            result.AddRange(cells);
            return result;
        }

        private static TableRow Row(params TableCell[] cells)
        {
            return new TableRow(cells);
        }

        private static TableCell XmlCell(string text, int span = 1)
        {
            var cell = new TableCell(ParagraphElement(text));
            if (span > 1)
            {
                cell.PrependChild(new TableCellProperties(new GridSpan { Val = span }));
            }

            return cell;
        }

        private static Paragraph ParagraphElement(string text)
        {
            return new Paragraph(new Run(new Text(text ?? string.Empty)));
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

        private static DocumentElement LegacyCell(
            int order,
            int tableIndex,
            int rowIndex,
            int cellIndex,
            string text)
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
                    new string[0]),
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
            int gridColumnCount,
            int logicalGridStart,
            int logicalGridSpan)
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
                    new string[0],
                    gridColumnCount,
                    logicalGridStart,
                    logicalGridSpan),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
