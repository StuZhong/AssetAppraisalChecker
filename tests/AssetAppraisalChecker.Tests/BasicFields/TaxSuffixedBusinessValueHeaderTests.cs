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
    public sealed class TaxSuffixedBusinessValueHeaderTests
    {
        private readonly BasicFieldExtractor _extractor = new BasicFieldExtractor();

        [TestMethod]
        [DataRow("评估净值（不含税）")]
        [DataRow("评估净值（含税）")]
        [DataRow("评估原值（不含税）")]
        [DataRow("评估原值（含税）")]
        [DataRow("评估净值(不含税)")]
        [DataRow("评估价值（不含增值税）")]
        [DataRow("账面价值（含增值税）")]
        public void Extract_SupportedTaxBasisSuffix_ConfirmsBusinessValueHeader(
            string businessHeader)
        {
            BasicFieldOccurrence occurrence = ProjectNames(
                SingleValueTable(0, 0, businessHeader, "测试项目甲")).Single();

            Assert.AreEqual("测试项目甲", occurrence.RawValue);
        }

        [TestMethod]
        public void Extract_MultipleTaxSuffixedBusinessColumns_ConfirmOneProjectName()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(
                WideTable(0, 0, "某限定项目名称", "测试项目甲"));

            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual("测试项目甲", occurrences[0].RawValue);
        }

        [TestMethod]
        public void Check_TwoEquivalentTaxSuffixedTablesWithSameProject_ReturnNoConflict()
        {
            List<DocumentElement> elements = WideTable(
                0, 0, "某限定项目名称", "测试项目甲");
            elements.AddRange(WideTable(
                1, elements.Count, "另一限定项目名称", "测试项目甲"));

            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(elements);
            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, occurrences.Count);
            Assert.AreEqual(1, occurrences
                .Select(item => item.NormalizedValue.CanonicalValue)
                .Distinct()
                .Count());
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_TwoEquivalentTaxSuffixedTablesWithDifferentProjects_ReturnSeriousConflict()
        {
            List<DocumentElement> elements = WideTable(
                0, 0, "某限定项目名称", "测试项目甲");
            elements.AddRange(WideTable(
                1, elements.Count, "另一限定项目名称", "测试项目乙"));

            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(elements);
            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, occurrences.Count);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueType.BasicFieldConflict, result.Issues[0].IssueType);
            Assert.AreEqual(IssueSeverity.Serious, result.Issues[0].Severity);
        }

        [TestMethod]
        [DataRow("项目名称（不含税）")]
        [DataRow("备注（含税）")]
        [DataRow("数量（不含税）")]
        [DataRow("日期（含税）")]
        [DataRow("税率（含税）")]
        [DataRow("说明（不含税）")]
        public void Extract_NonBusinessHeaderWithTaxSuffix_IsRejected(string header)
        {
            Assert.AreEqual(
                0,
                ProjectNames(SingleValueTable(0, 0, header, "测试项目甲")).Count,
                header);
        }

        [TestMethod]
        [DataRow("评估净值（不含税）说明")]
        [DataRow("关于评估净值（不含税）的说明")]
        public void Extract_TaxSuffixNotAtHeaderEnd_IsRejected(string header)
        {
            Assert.AreEqual(
                0,
                ProjectNames(SingleValueTable(0, 0, header, "测试项目甲")).Count,
                header);
        }

        [TestMethod]
        [DataRow("评估净值（参考）")]
        [DataRow("评估净值（暂估）")]
        [DataRow("评估净值（审计）")]
        [DataRow("评估净值（测试）")]
        public void Extract_UnknownParenthesizedSuffix_IsRejected(string header)
        {
            Assert.AreEqual(
                0,
                ProjectNames(SingleValueTable(0, 0, header, "测试项目甲")).Count,
                header);
        }

        [TestMethod]
        [DataRow("评估净值（不含税)")]
        [DataRow("评估净值(含税）")]
        public void Extract_MismatchedTaxSuffixParentheses_IsRejected(string header)
        {
            Assert.AreEqual(
                0,
                ProjectNames(SingleValueTable(0, 0, header, "测试项目甲")).Count,
                header);
        }

        [TestMethod]
        public void Extract_TaxSuffixedMultiDetailProjectTable_RemainsFailClosed()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(
                MultiDetailTable());

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_TaxSuffixedAggregateRow_NeverBecomesProjectName()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = ProjectNames(
                WideTable(0, 0, "某限定项目名称", "测试项目甲"));

            Assert.AreEqual(1, occurrences.Count);
            Assert.IsFalse(occurrences.Any(item => item.RawValue == "合计"));
        }

        [TestMethod]
        public void Extract_TaxSuffixedTable_LocationStillPointsToProjectValueCell()
        {
            BasicFieldOccurrence occurrence = ProjectNames(
                WideTable(3, 0, "某限定项目名称", "测试项目甲")).Single();

            Assert.AreEqual(3, occurrence.Location.TableIndex);
            Assert.AreEqual(1, occurrence.Location.RowIndex);
            Assert.AreEqual(0, occurrence.Location.CellIndex);
            Assert.AreEqual(0, occurrence.TextStartIndex);
            Assert.AreEqual("测试项目甲".Length, occurrence.TextLength);
        }

        [TestMethod]
        public void Extract_TaxSuffixedTable_DoesNotChangeStandaloneReportNumber()
        {
            var elements = new List<DocumentElement>
            {
                Paragraph(0, "云测试评报字〔2026〕第001号")
            };
            elements.AddRange(WideTable(
                0, elements.Count, "某限定项目名称", "测试项目甲"));

            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(elements);

            Assert.AreEqual(1, occurrences.Count(
                item => item.FieldType == BasicFieldType.ReportNumber));
        }

        [TestMethod]
        public void Extract_TaxSuffixedTable_DoesNotChangeValuationDate()
        {
            var elements = new List<DocumentElement>
            {
                Paragraph(0, "评估基准日：2026年6月30日")
            };
            elements.AddRange(WideTable(
                0, elements.Count, "某限定项目名称", "测试项目甲"));

            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(elements);
            Assert.AreEqual(1, occurrences.Count(
                item => item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_TaxSuffixedProjectName_DoesNotChangeMoneySubjects()
        {
            List<DocumentElement> elements = WideTable(
                0, 0, "某限定项目名称", "测试项目甲");
            elements.AddRange(WideTable(
                1, elements.Count, "另一限定项目名称", "测试项目甲"));
            var moneyExtractor = new MoneyOccurrenceExtractor();
            IReadOnlyList<MoneyOccurrence> before = moneyExtractor.Extract(elements);

            Assert.AreEqual(2, ProjectNames(elements).Count);
            IReadOnlyList<MoneyOccurrence> after = moneyExtractor.Extract(elements);

            CollectionAssert.AreEqual(
                before.Select(MoneySignature).ToArray(),
                after.Select(MoneySignature).ToArray());
            Assert.IsTrue(after
                .Where(item => item.Location.RowIndex == 1)
                .All(item => item.SubjectKind == SubjectKind.Asset));
        }

        [TestMethod]
        public void Extract_TaxSuffixedProjectName_DoesNotChangeMoneyComparison()
        {
            List<DocumentElement> elements = WideTable(
                0, 0, "某限定项目名称", "测试项目甲");
            elements.AddRange(WideTable(
                1, elements.Count, "另一限定项目名称", "测试项目甲"));
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

        private IReadOnlyList<BasicFieldOccurrence> ProjectNames(
            IEnumerable<DocumentElement> elements)
        {
            return _extractor.Extract(elements)
                .Where(item => item.FieldType == BasicFieldType.ProjectName)
                .ToList();
        }

        private static List<DocumentElement> SingleValueTable(
            int tableIndex,
            int initialOrder,
            string businessHeader,
            string projectName)
        {
            return new List<DocumentElement>
            {
                Cell(initialOrder, tableIndex, 0, 0, "某评估项目名称"),
                Cell(initialOrder + 1, tableIndex, 0, 1, businessHeader),
                Cell(initialOrder + 2, tableIndex, 1, 0, projectName),
                Cell(initialOrder + 3, tableIndex, 1, 1, "100万元"),
                Cell(initialOrder + 4, tableIndex, 2, 0, "合计"),
                Cell(initialOrder + 5, tableIndex, 2, 1, "100万元")
            };
        }

        private static List<DocumentElement> WideTable(
            int tableIndex,
            int initialOrder,
            string projectHeader,
            string projectName)
        {
            string[] headers =
            {
                "评估原值（不含税）",
                "评估净值（不含税）",
                "评估原值（含税）",
                "评估净值（含税）"
            };
            string[] detailAmounts =
            {
                "100万元", "90万元", "113万元", "101.7万元"
            };
            var elements = new List<DocumentElement>
            {
                Cell(initialOrder, tableIndex, 0, 0, projectHeader),
                Cell(initialOrder + 1, tableIndex, 0, 1, "说明")
            };
            int order = initialOrder + 2;
            for (int index = 0; index < headers.Length; index++)
            {
                elements.Add(Cell(order++, tableIndex, 0, index + 2, headers[index]));
            }

            elements.Add(Cell(order++, tableIndex, 1, 0, projectName));
            elements.Add(Cell(order++, tableIndex, 1, 1, "明细"));
            for (int index = 0; index < detailAmounts.Length; index++)
            {
                elements.Add(Cell(order++, tableIndex, 1, index + 2, detailAmounts[index]));
            }

            elements.Add(Cell(order++, tableIndex, 2, 0, string.Empty));
            elements.Add(Cell(order++, tableIndex, 2, 1, "合计"));
            for (int index = 0; index < detailAmounts.Length; index++)
            {
                elements.Add(Cell(order++, tableIndex, 2, index + 2, detailAmounts[index]));
            }

            return elements;
        }

        private static List<DocumentElement> MultiDetailTable()
        {
            return new List<DocumentElement>
            {
                Cell(0, 0, 0, 0, "项目名称"),
                Cell(1, 0, 0, 1, "评估净值（不含税）"),
                Cell(2, 0, 1, 0, "设备A"),
                Cell(3, 0, 1, 1, "100万元"),
                Cell(4, 0, 2, 0, "设备B"),
                Cell(5, 0, 2, 1, "200万元"),
                Cell(6, 0, 3, 0, "合计"),
                Cell(7, 0, 3, 1, "300万元")
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
                occurrence.FieldIdentity,
                occurrence.ComparisonSubjectKey,
                occurrence.NormalizedAmountYuan);
        }
    }
}
