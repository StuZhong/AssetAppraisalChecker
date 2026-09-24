using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class RepeatedSingleDetailSubjectTests
    {
        private readonly MoneyOccurrenceExtractor _extractor = new MoneyOccurrenceExtractor();

        [TestMethod]
        public void Extract_RepeatedSingleDetailBusinessTables_AssignSameAssetSubject()
        {
            List<DocumentElement> elements = TwoMatchingTables();

            IReadOnlyList<MoneyOccurrence> detail = DetailOccurrences(
                _extractor.Extract(elements));

            Assert.AreEqual(8, detail.Count);
            Assert.IsTrue(detail.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.AreEqual(1, detail.Select(item => item.SubjectId).Distinct().Count());
            Assert.IsTrue(detail.All(item => item.SubjectConfidence == ClassificationConfidence.High));
            Assert.IsTrue(detail.All(item => item.SubjectEvidence.Any(
                evidence => evidence.Contains("重复单明细业务表"))));
        }

        [TestMethod]
        public void Extract_AggregateLabelOutsideSubjectColumn_AssignsRepeatedAssetSubject()
        {
            IReadOnlyList<MoneyOccurrence> detail = DetailOccurrences(
                _extractor.Extract(TwoMatchingTablesWithSeparateAggregateLabel()));

            Assert.AreEqual(8, detail.Count);
            Assert.IsTrue(detail.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.AreEqual(1, detail.Select(item => item.SubjectId).Distinct().Count());
        }

        [TestMethod]
        public void Extract_HorizontallyMergedAggregateArea_AssignsRepeatedAssetSubject()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                CreateMergedAggregateTable("某限定项目名称"),
                CreateMergedAggregateTable("另一限定项目名称")))
            {
                DocumentReadResult document = new DocxDocumentReader().Read(file.FilePath);
                IReadOnlyList<MoneyOccurrence> detail = DetailOccurrences(
                    _extractor.Extract(document));

                Assert.IsTrue(document.IsSuccess);
                Assert.AreEqual(2, detail.Count);
                Assert.IsTrue(detail.All(item => item.SubjectKind == SubjectKind.Asset));
                Assert.AreEqual(1, detail.Select(item => item.SubjectId).Distinct().Count());
            }
        }

        [TestMethod]
        public void Extract_AggregateEvidenceRemainsStrictlyRowLocal()
        {
            List<DocumentElement> elements = CreateSingleDetailTableWithSeparateAggregateLabel(
                0, 0, "某限定项目名称", "测试项目A", StandardColumns(),
                includeAggregate: false);
            int insertionOrder = elements.Count;
            elements.Add(Cell(insertionOrder++, 0, 2, 0, string.Empty));
            elements.Add(Cell(insertionOrder++, 0, 2, 1, "普通说明"));
            elements.Add(Cell(insertionOrder++, 0, 2, 2, "100万元"));
            elements.Add(Cell(insertionOrder++, 0, 3, 0, string.Empty));
            elements.Add(Cell(insertionOrder++, 0, 3, 1, "合计"));
            elements.Add(Cell(insertionOrder++, 0, 3, 2, "100万元"));

            List<DocumentElement> second = CreateSingleDetailTableWithSeparateAggregateLabel(
                1, insertionOrder, "另一限定项目名称", "测试项目A", StandardColumns());
            elements.AddRange(second);

            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(elements);
            Assert.IsTrue(occurrences
                .Where(item => item.Location.TableIndex == 0 && item.Location.RowIndex == 2)
                .All(item => item.SubjectKind == SubjectKind.Unknown));
            Assert.IsTrue(occurrences
                .Where(item => item.Location.TableIndex == 0 && item.Location.RowIndex == 3)
                .All(item => item.SubjectKind == SubjectKind.Project));
        }

        [TestMethod]
        [DataRow("合计说明")]
        [DataRow("非合计项目")]
        [DataRow("与合计金额相比")]
        public void Extract_AggregateLikeNarrativeTextDoesNotActivateMode(string narrative)
        {
            List<DocumentElement> elements = CreateSingleDetailTableWithSeparateAggregateLabel(
                0, 0, "某限定项目名称", "测试项目A", StandardColumns(), narrative, false);
            elements.AddRange(CreateSingleDetailTableWithSeparateAggregateLabel(
                1, elements.Count, "另一限定项目名称", "测试项目A", StandardColumns(), narrative, false));

            Assert.IsTrue(DetailOccurrences(_extractor.Extract(elements))
                .All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_AggregateLabelWithoutMoneyValueDoesNotActivateMode()
        {
            List<DocumentElement> elements = CreateSingleDetailTableWithSeparateAggregateLabel(
                0, 0, "某限定项目名称", "测试项目A", StandardColumns(), "合计", false);
            elements.AddRange(CreateSingleDetailTableWithSeparateAggregateLabel(
                1, elements.Count, "另一限定项目名称", "测试项目A", StandardColumns(), "合计", false));

            Assert.IsTrue(DetailOccurrences(_extractor.Extract(elements))
                .All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_TwoValidAggregateRowsFailClosed()
        {
            List<DocumentElement> elements = CreateSingleDetailTableWithSeparateAggregateLabel(
                0, 0, "某限定项目名称", "测试项目A", StandardColumns(), "合计", true, true);
            elements.AddRange(CreateSingleDetailTableWithSeparateAggregateLabel(
                1, elements.Count, "另一限定项目名称", "测试项目A", StandardColumns(), "合计", true, true));

            Assert.IsTrue(DetailOccurrences(_extractor.Extract(elements))
                .All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_RepeatedSingleDetailHeadersMayUseDifferentQualifiedPrefixes()
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0,
                0,
                "工程项目名称",
                "测试项目A",
                StandardColumns());
            elements.AddRange(CreateSingleDetailTable(
                1,
                elements.Count,
                "费用项目名称",
                "测试项目A",
                StandardColumns()));

            Assert.IsTrue(DetailOccurrences(_extractor.Extract(elements))
                .All(item => item.SubjectKind == SubjectKind.Asset));
        }

        [TestMethod]
        public void Extract_RepeatedSubjectUsesExactNormalizedMatch()
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0,
                0,
                "工程项目名称",
                "  测试　项目A  ",
                StandardColumns());
            elements.AddRange(CreateSingleDetailTable(
                1,
                elements.Count,
                "评估项目名称",
                "测试 项目A",
                StandardColumns()));

            IReadOnlyList<MoneyOccurrence> detail = DetailOccurrences(
                _extractor.Extract(elements));

            Assert.IsTrue(detail.All(item => item.SubjectId == "测试 项目A"));
        }

        [TestMethod]
        public void Extract_DifferentSingleDetailSubjectsRemainUnknown()
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0,
                0,
                "工程项目名称",
                "测试项目A",
                StandardColumns());
            elements.AddRange(CreateSingleDetailTable(
                1,
                elements.Count,
                "工程项目名称",
                "测试项目B",
                StandardColumns()));

            IReadOnlyList<MoneyOccurrence> detail = DetailOccurrences(
                _extractor.Extract(elements));

            Assert.IsTrue(detail.All(item => item.SubjectKind == SubjectKind.Unknown));
            Assert.AreEqual(0, new ConsistencyEngine().Check(detail).ComparisonGroups.Count);
        }

        [TestMethod]
        public void Extract_ContainedButDifferentSubjectsDoNotMatch()
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0,
                0,
                "工程项目名称",
                "测试项目A",
                StandardColumns());
            elements.AddRange(CreateSingleDetailTable(
                1,
                elements.Count,
                "工程项目名称",
                "测试项目A一期",
                StandardColumns()));

            Assert.IsTrue(DetailOccurrences(_extractor.Extract(elements))
                .All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_ThreeTablesAAB_OnlyRepeatedSubjectIsConfirmed()
        {
            List<DocumentElement> elements = CreateSingleDetailTableWithSeparateAggregateLabel(
                0, 0, "工程项目名称", "测试项目A", StandardColumns());
            elements.AddRange(CreateSingleDetailTableWithSeparateAggregateLabel(
                1, elements.Count, "费用项目名称", "测试项目A", StandardColumns()));
            elements.AddRange(CreateSingleDetailTableWithSeparateAggregateLabel(
                2, elements.Count, "资产项目名称", "测试项目B", StandardColumns()));

            IReadOnlyList<MoneyOccurrence> detail = DetailOccurrences(
                _extractor.Extract(elements));
            IReadOnlyList<MoneyOccurrence> subjectA = detail
                .Where(item => item.Location.TableIndex == 0 || item.Location.TableIndex == 1)
                .ToList();
            IReadOnlyList<MoneyOccurrence> subjectB = detail
                .Where(item => item.Location.TableIndex == 2)
                .ToList();

            Assert.IsTrue(subjectA.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.IsTrue(subjectB.All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_TablesWithoutSharedMoneyCategoryRemainUnknown()
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0,
                0,
                "工程项目名称",
                "测试项目A",
                new[] { Column("不含税评估净值", "90万元", "90万元") });
            elements.AddRange(CreateSingleDetailTable(
                1,
                elements.Count,
                "工程项目名称",
                "测试项目A",
                new[] { Column("不含税评估原值", "100万元", "100万元") }));

            Assert.IsTrue(DetailOccurrences(_extractor.Extract(elements))
                .All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_OneTableWithoutAggregateRowPreventsRepeatedMode()
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0, 0, "工程项目名称", "测试项目A", StandardColumns());
            elements.AddRange(CreateSingleDetailTable(
                1,
                elements.Count,
                "工程项目名称",
                "测试项目A",
                StandardColumns(),
                includeAggregate: false));

            Assert.IsTrue(DetailOccurrences(_extractor.Extract(elements))
                .All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_OnlyOneSingleDetailBusinessTableRemainsUnknown()
        {
            IReadOnlyList<MoneyOccurrence> detail = DetailOccurrences(
                _extractor.Extract(CreateSingleDetailTable(
                    0, 0, "工程项目名称", "测试项目A", StandardColumns())));

            Assert.IsTrue(detail.All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_TwoRepeatedMetadataTablesDoNotCreateMoneySubjects()
        {
            var elements = new List<DocumentElement>
            {
                Cell(0, 0, 0, 0, "评估项目名称"),
                Cell(1, 0, 0, 1, "XX测试项目"),
                Cell(2, 1, 0, 0, "工程项目名称"),
                Cell(3, 1, 0, 1, "XX测试项目")
            };

            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(elements);

            Assert.AreEqual(0, occurrences.Count);
            Assert.AreEqual(0, new ConsistencyEngine().Check(occurrences).ComparisonGroups.Count);
        }

        [TestMethod]
        [DataRow("项目")]
        [DataRow("资产")]
        [DataRow("评估对象")]
        public void Extract_RepeatedGenericBusinessTermNeverBecomesDetailSubject(string subject)
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0, 0, "工程项目名称", subject, StandardColumns());
            elements.AddRange(CreateSingleDetailTable(
                1, elements.Count, "费用项目名称", subject, StandardColumns()));

            Assert.IsTrue(DetailOccurrences(_extractor.Extract(elements))
                .All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_ProjectNameCandidateCannotPairWithOrdinaryNameColumn()
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0, 0, "工程项目名称", "测试项目A", StandardColumns());
            elements.AddRange(CreateSingleDetailTable(
                1, elements.Count, "名称", "测试项目A", StandardColumns()));

            IReadOnlyList<MoneyOccurrence> detail = DetailOccurrences(
                _extractor.Extract(elements));

            Assert.IsTrue(detail.Where(item => item.Location.TableIndex == 0)
                .All(item => item.SubjectKind == SubjectKind.Unknown));
            Assert.IsTrue(detail.Where(item => item.Location.TableIndex == 1)
                .All(item => item.SubjectKind == SubjectKind.Asset));
        }

        [TestMethod]
        public void Extract_AggregateRowsRemainProjectAndNeverAsset()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(
                TwoMatchingTables());
            IReadOnlyList<MoneyOccurrence> aggregate = AggregateOccurrences(occurrences);

            Assert.AreEqual(8, aggregate.Count);
            Assert.IsTrue(aggregate.All(item => item.SubjectKind == SubjectKind.Project));
            Assert.IsFalse(aggregate.Any(item => item.SubjectId == "合计"));
            Assert.IsTrue(aggregate.All(item => item.FieldIdentity.Scope == Scope.Subtotal));
        }

        [TestMethod]
        public void Check_RepeatedFourFieldDetailsRecoverFourSafeGroups()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = _extractor.Extract(
                TwoMatchingTablesWithSeparateAggregateLabel());
            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);
            IReadOnlyList<ComparisonGroup> detailGroups = result.ComparisonGroups
                .Where(group => group.ComparisonKey.ComparisonSubjectKey.SubjectKind == SubjectKind.Asset)
                .ToList();

            Assert.AreEqual(4, detailGroups.Count);
            Assert.IsTrue(detailGroups.All(group => group.Occurrences.Count == 2));
            Assert.AreEqual(4, detailGroups.Select(group => new
            {
                group.ComparisonKey.ValueCategory,
                group.ComparisonKey.TaxBasis
            }).Distinct().Count());
            Assert.IsTrue(detailGroups.All(group => group.ComparisonKey.Scope == Scope.Unspecified));
            Assert.AreEqual(0, result.Issues.Count);
            Assert.IsFalse(result.ComparisonGroups.Any(group =>
                group.Occurrences.Select(item => item.SubjectKind).Distinct().Count() > 1));
        }

        [TestMethod]
        public void Check_RepeatedSingleDetailDifferentAmountRestoresConflict()
        {
            List<DocumentElement> elements = CreateSingleDetailTableWithSeparateAggregateLabel(
                0, 0, "工程项目名称", "测试项目A", StandardColumns());
            TableColumn[] changed = StandardColumns();
            changed[1] = Column("不含税评估净值", "80万元", "90万元");
            elements.AddRange(CreateSingleDetailTableWithSeparateAggregateLabel(
                1, elements.Count, "费用项目名称", "测试项目A", changed));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(
                _extractor.Extract(elements));

            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueType.MoneyConflict, result.Issues[0].IssueType);
            Assert.AreEqual(100000m, result.Issues[0].Difference);
            Assert.AreEqual(SubjectKind.Asset,
                result.Issues[0].ComparisonKey.ComparisonSubjectKey.SubjectKind);
        }

        [TestMethod]
        public void Check_DifferentSingleDetailSubjectsProduceNoConflict()
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0, 0, "工程项目名称", "测试项目A", StandardColumns());
            elements.AddRange(CreateSingleDetailTable(
                1, elements.Count, "工程项目名称", "测试项目B", StandardColumns()));

            ConsistencyCheckResult result = new ConsistencyEngine().Check(
                _extractor.Extract(elements));

            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(8, result.SkippedOccurrenceCount);
        }

        [TestMethod]
        public void Check_RepeatedSingleDetailUsesExistingComparisonKeyAcrossSections()
        {
            IReadOnlyList<ComparisonGroup> detailGroups = new ConsistencyEngine()
                .Check(_extractor.Extract(TwoMatchingTables()))
                .ComparisonGroups
                .Where(group => group.ComparisonKey.ComparisonSubjectKey.SubjectKind == SubjectKind.Asset)
                .ToList();

            Assert.IsTrue(detailGroups.All(group =>
                group.Occurrences.Select(item => item.FieldIdentity.SectionCategory)
                    .Distinct().Count() == 2));
            Assert.IsTrue(detailGroups.All(group =>
                group.ComparisonKey.ComparisonSubjectKey.NormalizedSubjectId == "测试项目A"));
        }

        private static List<DocumentElement> TwoMatchingTables()
        {
            List<DocumentElement> elements = CreateSingleDetailTable(
                0, 0, "某限定项目名称", "测试项目A", StandardColumns());
            elements.AddRange(CreateSingleDetailTable(
                1, elements.Count, "另一限定项目名称", "测试项目A", StandardColumns()));
            return elements;
        }

        private static List<DocumentElement> TwoMatchingTablesWithSeparateAggregateLabel()
        {
            List<DocumentElement> elements = CreateSingleDetailTableWithSeparateAggregateLabel(
                0, 0, "某限定项目名称", "测试项目A", StandardColumns());
            elements.AddRange(CreateSingleDetailTableWithSeparateAggregateLabel(
                1, elements.Count, "另一限定项目名称", "测试项目A", StandardColumns()));
            return elements;
        }

        private static TableColumn[] StandardColumns()
        {
            return new[]
            {
                Column("不含税评估原值", "100万元", "100万元"),
                Column("不含税评估净值", "90万元", "90万元"),
                Column("含税评估原值", "113万元", "113万元"),
                Column("含税评估净值", "101.7万元", "101.7万元")
            };
        }

        private static TableColumn Column(
            string header,
            string detailAmount,
            string aggregateAmount)
        {
            return new TableColumn(header, detailAmount, aggregateAmount);
        }

        private static List<DocumentElement> CreateSingleDetailTable(
            int table,
            int initialOrder,
            string subjectHeader,
            string subject,
            IReadOnlyList<TableColumn> columns,
            bool includeAggregate = true)
        {
            var elements = new List<DocumentElement>
            {
                Cell(initialOrder, table, 0, 0, subjectHeader)
            };
            int order = initialOrder + 1;
            for (int index = 0; index < columns.Count; index++)
            {
                elements.Add(Cell(order++, table, 0, index + 1, columns[index].Header));
            }

            elements.Add(Cell(order++, table, 1, 0, subject));
            for (int index = 0; index < columns.Count; index++)
            {
                elements.Add(Cell(order++, table, 1, index + 1, columns[index].DetailAmount));
            }

            if (includeAggregate)
            {
                elements.Add(Cell(order++, table, 2, 0, "合计"));
                for (int index = 0; index < columns.Count; index++)
                {
                    elements.Add(Cell(order++, table, 2, index + 1, columns[index].AggregateAmount));
                }
            }

            return elements;
        }

        private static List<DocumentElement> CreateSingleDetailTableWithSeparateAggregateLabel(
            int table,
            int initialOrder,
            string subjectHeader,
            string subject,
            IReadOnlyList<TableColumn> columns,
            string aggregateLabel = "合计",
            bool includeAggregateAmounts = true,
            bool includeSecondAggregate = false,
            bool includeAggregate = true)
        {
            var elements = new List<DocumentElement>
            {
                Cell(initialOrder, table, 0, 0, subjectHeader),
                Cell(initialOrder + 1, table, 0, 1, "说明")
            };
            int order = initialOrder + 2;
            for (int index = 0; index < columns.Count; index++)
            {
                elements.Add(Cell(order++, table, 0, index + 2, columns[index].Header));
            }

            elements.Add(Cell(order++, table, 1, 0, subject));
            elements.Add(Cell(order++, table, 1, 1, "明细"));
            for (int index = 0; index < columns.Count; index++)
            {
                elements.Add(Cell(order++, table, 1, index + 2, columns[index].DetailAmount));
            }

            if (includeAggregate)
            {
                AddAggregateRow(elements, table, 2, ref order, columns, aggregateLabel,
                    includeAggregateAmounts);
                if (includeSecondAggregate)
                {
                    AddAggregateRow(elements, table, 3, ref order, columns, "总计", true);
                }
            }

            return elements;
        }

        private static void AddAggregateRow(
            ICollection<DocumentElement> elements,
            int table,
            int row,
            ref int order,
            IReadOnlyList<TableColumn> columns,
            string aggregateLabel,
            bool includeAmounts)
        {
            elements.Add(Cell(order++, table, row, 0, string.Empty));
            elements.Add(Cell(order++, table, row, 1, aggregateLabel));
            for (int index = 0; index < columns.Count; index++)
            {
                elements.Add(Cell(
                    order++,
                    table,
                    row,
                    index + 2,
                    includeAmounts ? columns[index].AggregateAmount : string.Empty));
            }
        }

        private static Table CreateMergedAggregateTable(string subjectHeader)
        {
            return new Table(
                WordRow(
                    WordCell(subjectHeader),
                    WordCell("说明一"),
                    WordCell("说明二"),
                    WordCell("不含税评估净值")),
                WordRow(
                    WordCell("测试项目A"),
                    WordCell("明细"),
                    WordCell(string.Empty),
                    WordCell("90万元")),
                WordRow(
                    WordCell(string.Empty),
                    MergedWordCell("合计", MergedCellValues.Restart),
                    MergedWordCell(string.Empty, MergedCellValues.Continue),
                    WordCell("90万元")));
        }

        private static TableRow WordRow(params TableCell[] cells)
        {
            return new TableRow(cells);
        }

        private static TableCell WordCell(string text)
        {
            return new TableCell(new Paragraph(new Run(new Text(text ?? string.Empty))));
        }

        private static TableCell MergedWordCell(string text, MergedCellValues mergeValue)
        {
            return new TableCell(
                new TableCellProperties(new HorizontalMerge { Val = mergeValue }),
                new Paragraph(new Run(new Text(text ?? string.Empty))));
        }

        private static IReadOnlyList<MoneyOccurrence> DetailOccurrences(
            IEnumerable<MoneyOccurrence> occurrences)
        {
            return occurrences.Where(item => item.Location.RowIndex == 1).ToList();
        }

        private static IReadOnlyList<MoneyOccurrence> AggregateOccurrences(
            IEnumerable<MoneyOccurrence> occurrences)
        {
            return occurrences.Where(item => item.Location.RowIndex == 2).ToList();
        }

        private static DocumentElement Cell(
            int order,
            int table,
            int row,
            int cell,
            string text)
        {
            string heading = table == 0 ? "报告摘要" : "评估结论";
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

        private sealed class TableColumn
        {
            public TableColumn(string header, string detailAmount, string aggregateAmount)
            {
                Header = header;
                DetailAmount = detailAmount;
                AggregateAmount = aggregateAmount;
            }

            public string Header { get; }

            public string DetailAmount { get; }

            public string AggregateAmount { get; }
        }
    }
}
