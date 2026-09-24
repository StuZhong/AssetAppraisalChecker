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
    public sealed class ConsistencyDocxIntegrationTests
    {
        [TestMethod]
        public void FullPipeline_SyntheticConflictingDocx_ReturnsOneConflictAndSuspectedOutlier()
        {
            using (SyntheticDocxFile file = CreateDocument(
                "本项目不含税评估净值为170万元。",
                "经评估，本项目不含税评估净值为160万元。",
                "170.00万元"))
            {
                IReadOnlyList<MoneyOccurrence> occurrences = ReadOccurrences(file);

                ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

                Assert.AreEqual(3, occurrences.Count);
                Assert.AreEqual(1, result.ComparisonGroups.Count);
                Assert.AreEqual(3, result.ComparisonGroups[0].Occurrences.Count);
                Assert.AreEqual(1, result.Issues.Count);
                Assert.AreEqual(IssueType.MoneyConflict, result.Issues[0].IssueType);
                Assert.AreEqual(1600000m, result.Issues[0].SuspectedOutlierOccurrences.Single().NormalizedAmountYuan);
                Assert.IsTrue(occurrences.Any(item => item.FieldIdentity.SectionCategory == SectionCategory.ReportSummary));
                Assert.IsTrue(occurrences.Any(item => item.FieldIdentity.SectionCategory == SectionCategory.ValuationConclusion));
                Assert.IsTrue(occurrences.Any(item => item.FieldIdentity.SectionCategory == SectionCategory.ResultTable));
            }
        }

        [TestMethod]
        public void FullPipeline_SyntheticConsistentDocx_ReturnsNoIssue()
        {
            using (SyntheticDocxFile file = CreateDocument(
                "本项目不含税评估净值为170万元。",
                "经评估，本项目不含税评估净值为人民币壹佰柒拾万元整。",
                "1,700,000元"))
            {
                IReadOnlyList<MoneyOccurrence> occurrences = ReadOccurrences(file);

                ConsistencyCheckResult result = new ConsistencyEngine().Check(occurrences);

                Assert.AreEqual(3, occurrences.Count);
                Assert.AreEqual(1, result.ComparisonGroups.Count);
                Assert.AreEqual(0, result.Issues.Count);
                Assert.IsTrue(occurrences.All(item => item.NormalizedAmountYuan == 1700000m));
            }
        }

        private static IReadOnlyList<MoneyOccurrence> ReadOccurrences(SyntheticDocxFile file)
        {
            var reader = new DocxDocumentReader();
            DocumentReadResult document = reader.Read(file.FilePath);
            Assert.IsTrue(document.IsSuccess, document.Message);
            return new MoneyOccurrenceExtractor().Extract(document);
        }

        private static SyntheticDocxFile CreateDocument(
            string summaryAmount,
            string conclusionAmount,
            string tableAmount)
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
                StyledParagraph("Heading2", "评估摘要"),
                ParagraphWithText(summaryAmount),
                StyledParagraph("Heading2", "评估结论"),
                ParagraphWithText(conclusionAmount),
                StyledParagraph("Heading2", "评估结果表"),
                new Table(
                    Row("项目", "不含税评估净值"),
                    Row("项目合计", tableAmount)));
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
