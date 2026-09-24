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

namespace AssetAppraisalChecker.Tests.Consistency
{
    [TestClass]
    public sealed class Phase41DocxIntegrationTests
    {
        [TestMethod]
        public void FullPipeline_DifferentAssetsInDetailTable_ReturnsNoConflict()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("金额单位：万元"),
                new Table(
                    Row("序号", "资产名称", "数量", "不含税评估净值"),
                    Row("1", "测试设备A", "10", "50"),
                    Row("2", "测试设备B", "20", "80"),
                    Row("3", "测试设备C", "30", "30"))))
            {
                IReadOnlyList<MoneyOccurrence> occurrences = ReadOccurrences(file);

                ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

                CollectionAssert.AreEqual(
                    new[] { "50", "80", "30" },
                    occurrences.Select(item => item.RawText).ToArray());
                CollectionAssert.AreEqual(
                    new[] { 500000m, 800000m, 300000m },
                    occurrences.Select(item => item.NormalizedAmountYuan).ToArray());
                CollectionAssert.AreEqual(
                    new[] { "测试设备A", "测试设备B", "测试设备C" },
                    occurrences.Select(item => item.SubjectId).ToArray());
                Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Asset));
                Assert.IsTrue(occurrences.All(item => item.SubjectConfidence == ClassificationConfidence.High));
                Assert.IsTrue(occurrences.All(item => item.FieldIdentity.Scope == Scope.Unspecified));
                Assert.AreEqual(3, result.ComparisonGroups.Count);
                Assert.AreEqual(0, result.Issues.Count);
            }
        }

        [TestMethod]
        public void FullPipeline_ProjectTotalAcrossSections_ReturnsOneConflictAndOutlier()
        {
            using (SyntheticDocxFile file = CreateProjectConflictDocument())
            {
                IReadOnlyList<MoneyOccurrence> occurrences = ReadOccurrences(file);

                ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

                Assert.AreEqual(3, occurrences.Count);
                Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Project));
                Assert.IsTrue(occurrences.All(item => item.SubjectId == "PROJECT"));
                Assert.IsTrue(occurrences.All(item => item.FieldIdentity.Scope == Scope.ProjectTotal));
                Assert.AreEqual(3, occurrences.Select(item => item.FieldIdentity.SectionCategory).Distinct().Count());
                Assert.AreEqual(1, result.ComparisonGroups.Count);
                Assert.AreEqual(3, result.ComparisonGroups[0].Occurrences.Count);
                Assert.AreEqual(1, result.Issues.Count);
                Assert.AreEqual(
                    1600000m,
                    result.Issues[0].SuspectedOutlierOccurrences.Single().NormalizedAmountYuan);
            }
        }

        [TestMethod]
        public void FullPipeline_SameAssetAcrossTwoTables_ProducesConflict()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                new Table(
                    Row("资产名称", "评估净值"),
                    Row("测试设备A", "50万元")),
                ParagraphWithText("以下为复核表（完全虚构）"),
                new Table(
                    Row("资产名称", "评估净值"),
                    Row("测试设备A", "55万元"))))
            {
                IReadOnlyList<MoneyOccurrence> occurrences = ReadOccurrences(file);

                ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

                Assert.AreEqual(2, occurrences.Count);
                Assert.IsTrue(occurrences.All(item => item.SubjectId == "测试设备A"));
                Assert.AreEqual(2, occurrences.Select(item => item.Location.TableIndex).Distinct().Count());
                Assert.AreEqual(1, result.ComparisonGroups.Count);
                Assert.AreEqual(1, result.Issues.Count);
                Assert.AreEqual(50000m, result.Issues[0].Difference);
                Assert.AreEqual(0, result.Issues[0].SuspectedOutlierOccurrences.Count);
            }
        }

        [TestMethod]
        public void FullPipeline_TableWithoutSubjectColumn_IsFailClosedAcrossRows()
        {
            using (SyntheticDocxFile file = SyntheticDocxFile.Create(
                ParagraphWithText("金额单位：万元"),
                new Table(
                    Row("不含税评估净值"),
                    Row("50"),
                    Row("80"),
                    Row("30"))))
            {
                IReadOnlyList<MoneyOccurrence> occurrences = ReadOccurrences(file);

                ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

                Assert.AreEqual(3, occurrences.Count);
                Assert.IsTrue(occurrences.All(item => item.SubjectKind == SubjectKind.Unknown));
                Assert.AreEqual(0, result.ComparisonGroups.Count);
                Assert.AreEqual(0, result.Issues.Count);
                Assert.AreEqual(3, result.SkippedOccurrenceCount);
            }
        }

        private static IReadOnlyList<MoneyOccurrence> ReadOccurrences(SyntheticDocxFile file)
        {
            var reader = new DocxDocumentReader();
            DocumentReadResult document = reader.Read(file.FilePath);
            Assert.IsTrue(document.IsSuccess, document.Message);
            return new MoneyOccurrenceExtractor().Extract(document);
        }

        private static SyntheticDocxFile CreateProjectConflictDocument()
        {
            var styles = new List<Style>
            {
                new Style(new StyleName { Val = "heading 1" })
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "Heading1"
                },
                new Style(new StyleName { Val = "heading 2" })
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "Heading2"
                }
            };

            return SyntheticDocxFile.CreateWithStyles(
                styles,
                StyledParagraph("Heading1", "XX测试资产评估项目（完全虚构）"),
                StyledParagraph("Heading2", "报告摘要"),
                ParagraphWithText("本项目不含税评估净值170万元"),
                StyledParagraph("Heading2", "评估结论"),
                ParagraphWithText("本项目不含税评估净值160万元"),
                StyledParagraph("Heading2", "评估结果表"),
                new Table(
                    Row("项目", "不含税评估净值"),
                    Row("项目合计", "170万元")));
        }

        private static Paragraph StyledParagraph(string styleId, string text)
        {
            return new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
                new Run(new Text(text)));
        }

        private static Paragraph ParagraphWithText(string text)
        {
            return new Paragraph(new Run(new Text(text)));
        }

        private static TableRow Row(params string[] cellTexts)
        {
            return new TableRow(cellTexts.Select(text => new TableCell(ParagraphWithText(text))));
        }
    }
}
