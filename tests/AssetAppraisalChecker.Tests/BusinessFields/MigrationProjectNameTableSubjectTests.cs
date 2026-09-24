using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BusinessFields
{
    [TestClass]
    public sealed class MigrationProjectNameTableSubjectTests
    {
        private const string AuditedHeader = "迁改项目名称";

        private readonly MoneyOccurrenceExtractor extractor =
            new MoneyOccurrenceExtractor();

        [TestMethod]
        public void Extract_AuditedHeaderAndFourMoneyDimensions_ShareOneAssetSubject()
        {
            string longVirtualSubject = "虚构迁改标的" + new string('甲', 81);
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(
                AuditedProductionShapeTable(longVirtualSubject));
            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(4, occurrences.Count);
            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.AreEqual(1, occurrences.Select(item => item.SubjectId).Distinct().Count());
            Assert.AreEqual(2, occurrences.Select(item => item.FieldIdentity.ValueCategory).Distinct().Count());
            Assert.AreEqual(2, occurrences.Select(item => item.FieldIdentity.TaxBasis).Distinct().Count());
            Assert.AreEqual(4, result.ComparisonGroups.Count);
            Assert.IsTrue(result.ComparisonGroups.All(group => group.Occurrences.Count == 1));
        }

        [TestMethod]
        public void Extract_MigrationSubjectResolution_DoesNotIncreaseBasicFieldOccurrences()
        {
            IReadOnlyList<DocumentElement> elements = FourValueTable(
                AuditedHeader,
                "虚构迁改标的一");
            BasicFieldExtractor basicExtractor = new BasicFieldExtractor();
            int before = basicExtractor.Extract(elements)
                .Count(item => item.FieldType == BasicFieldType.ProjectName);

            Assert.IsTrue(extractor.Extract(elements)
                .All(item => item.SubjectKind == SubjectKind.Asset));
            int after = basicExtractor.Extract(elements)
                .Count(item => item.FieldType == BasicFieldType.ProjectName);
            Assert.AreEqual(before, after);
        }

        [TestMethod]
        public void Extract_MigrationProjectHeader_DoesNotCreateBasicFieldProjectName()
        {
            IReadOnlyList<DocumentElement> elements = AuditedProductionShapeTable(
                "虚构迁改标的一");

            Assert.AreEqual(
                0,
                new BasicFieldExtractor().Extract(elements).Count(item =>
                    item.FieldType == BasicFieldType.ProjectName));
        }

        [TestMethod]
        public void Extract_MigrationProjectHeader_PreservesAssetSubjectAndGrouping()
        {
            IReadOnlyList<DocumentElement> elements = AuditedProductionShapeTable(
                "虚构迁改标的一");
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(elements);
            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(4, occurrences.Count);
            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.AreEqual(1, occurrences.Select(item => item.SubjectId).Distinct().Count());
            Assert.AreEqual(4, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Check_MigrationProjectHeader_PreservesMoneyConflictDetection()
        {
            var elements = new List<DocumentElement>();
            elements.AddRange(SingleMigrationValueTable(
                0,
                0,
                "虚构迁改标的一",
                "50万元"));
            elements.AddRange(SingleMigrationValueTable(
                1,
                elements.Count,
                "虚构迁改标的一",
                "60万元"));

            Assert.AreEqual(
                0,
                new BasicFieldExtractor().Extract(elements).Count(item =>
                    item.FieldType == BasicFieldType.ProjectName));

            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(elements);
            ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, occurrences.Count);
            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Asset));
            Assert.AreEqual(1, occurrences.Select(item => item.SubjectId).Distinct().Count());
            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(2, result.ComparisonGroups[0].Occurrences.Count);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueType.MoneyConflict, result.Issues[0].IssueType);
        }

        [TestMethod]
        public void Extract_GenericProjectNameHeader_DoesNotActivateMigrationRule()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(
                FourValueTable("项目名称", "虚构迁改标的一"));

            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_QualifiedMigrationProjectNameHeader_FailsClosed()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(
                FourValueTable("其他迁改项目名称", "虚构迁改标的一"));

            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_CompetingSubjectColumns_FailsClosed()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(new[]
            {
                Cell(0, 0, 0, AuditedHeader),
                Cell(1, 0, 1, "资产名称"),
                Cell(2, 0, 2, "评估净值"),
                Cell(3, 1, 0, "虚构迁改标的一"),
                Cell(4, 1, 1, "虚构设备一"),
                Cell(5, 1, 2, "50万元")
            });

            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_TwoTextCandidatesInDetailRow_FailsClosed()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(new[]
            {
                Cell(0, 0, 0, AuditedHeader),
                Cell(1, 0, 1, "说明"),
                Cell(2, 0, 2, "评估净值"),
                Cell(3, 1, 0, "虚构迁改标的一"),
                Cell(4, 1, 1, "虚构说明文本"),
                Cell(5, 1, 2, "50万元")
            });

            Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Unknown));
        }

        [TestMethod]
        public void Extract_EmptySubject_FailsClosed()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable(string.Empty, "50万元")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_NumericSubject_FailsClosed()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable("123", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Unknown, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_MoneySubject_FailsClosed()
        {
            IReadOnlyList<MoneyOccurrence> occurrences = extractor.Extract(
                SingleValueTable("50万元", "60万元"));
            MoneyOccurrence target = occurrences.Single(item => item.Location.CellIndex == 1);

            Assert.AreEqual(SubjectKind.Unknown, target.SubjectKind);
        }

        [TestMethod]
        public void Extract_ProjectTotalRow_DoesNotUseDetailSubjectRule()
        {
            MoneyOccurrence occurrence = extractor.Extract(
                SingleValueTable("总计", "50万元")).Single();

            Assert.AreEqual(SubjectKind.Project, occurrence.SubjectKind);
            Assert.AreEqual(Scope.ProjectTotal, occurrence.FieldIdentity.Scope);
        }

        [TestMethod]
        public void Extract_NoTableDocument_RemainsUnaffected()
        {
            DocumentElement paragraph = Paragraph(
                "本项目不含税评估净值为100万元。");
            MoneyOccurrence occurrence = extractor.Extract(new[] { paragraph }).Single();

            Assert.AreEqual(SubjectKind.Project, occurrence.SubjectKind);
        }

        [TestMethod]
        public void Extract_ExistingLineNameSemantic_RemainsSupported()
        {
            IReadOnlyList<DocumentElement> elements = FourValueTable(
                "名称（分项/线路名称）",
                "虚构线路一");

            Assert.IsTrue(extractor.Extract(elements)
                .All(item => item.SubjectKind == SubjectKind.Asset));
        }

        private static IReadOnlyList<DocumentElement> FourValueTable(
            string header,
            string subject)
        {
            return new[]
            {
                Cell(0, 0, 0, header),
                Cell(1, 0, 1, "不含税评估原值"),
                Cell(2, 0, 2, "不含税评估净值"),
                Cell(3, 0, 3, "含税评估原值"),
                Cell(4, 0, 4, "含税评估净值"),
                Cell(5, 1, 0, subject),
                Cell(6, 1, 1, "10万元"),
                Cell(7, 1, 2, "9万元"),
                Cell(8, 1, 3, "11.3万元"),
                Cell(9, 1, 4, "10.17万元")
            };
        }

        private static IReadOnlyList<DocumentElement> AuditedProductionShapeTable(
            string subject)
        {
            return new[]
            {
                Cell(0, 0, 0, "序号"),
                Cell(1, 0, 1, AuditedHeader),
                Cell(2, 0, 2, "数量"),
                Cell(3, 0, 3, "不含税评估原值"),
                Cell(4, 0, 4, "不含税评估净值"),
                Cell(5, 0, 5, "含税评估原值"),
                Cell(6, 0, 6, "含税评估净值"),
                Cell(7, 1, 0, "1"),
                Cell(8, 1, 1, subject),
                Cell(9, 1, 2, "1项"),
                Cell(10, 1, 3, "10万元"),
                Cell(11, 1, 4, "9万元"),
                Cell(12, 1, 5, "11.3万元"),
                Cell(13, 1, 6, "10.17万元")
            };
        }

        private static IReadOnlyList<DocumentElement> SingleValueTable(
            string subject,
            string amount)
        {
            return new[]
            {
                Cell(0, 0, 0, AuditedHeader),
                Cell(1, 0, 1, "评估净值"),
                Cell(2, 1, 0, subject),
                Cell(3, 1, 1, amount)
            };
        }

        private static IReadOnlyList<DocumentElement> SingleMigrationValueTable(
            int tableIndex,
            int initialOrder,
            string subject,
            string amount)
        {
            return new[]
            {
                Cell(initialOrder, tableIndex, 0, 0, AuditedHeader),
                Cell(initialOrder + 1, tableIndex, 0, 1, "评估净值"),
                Cell(initialOrder + 2, tableIndex, 1, 0, subject),
                Cell(initialOrder + 3, tableIndex, 1, 1, amount)
            };
        }

        private static DocumentElement Paragraph(string text)
        {
            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    0,
                    null,
                    null,
                    null,
                    new string[0]),
                0,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }

        private static DocumentElement Cell(int order, int row, int cell, string text)
        {
            return Cell(order, 0, row, cell, text);
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
                    new string[0]),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
