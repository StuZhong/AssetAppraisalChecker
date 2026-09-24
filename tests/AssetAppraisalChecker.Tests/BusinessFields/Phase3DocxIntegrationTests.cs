using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Document;
using AssetAppraisalChecker.Tests.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BusinessFields
{
    [TestClass]
    public sealed class Phase3DocxIntegrationTests
    {
        [TestMethod]
        public void ReadAndExtract_SyntheticAssessmentDocx_ReturnsClassifiedAmountsOnly()
        {
            using (SyntheticDocxFile file = CreateSyntheticAssessmentDocument())
            {
                var reader = new DocxDocumentReader();
                var occurrenceExtractor = new MoneyOccurrenceExtractor();

                DocumentReadResult document = reader.Read(file.FilePath);
                IReadOnlyList<MoneyOccurrence> occurrences = occurrenceExtractor.Extract(document);

                Assert.IsTrue(document.IsSuccess);
                Assert.AreEqual(6, occurrences.Count);
                Assert.IsFalse(occurrences.Any(item => item.RawText == "1" || item.RawText == "10"));
                Assert.AreEqual(3, occurrences.Count(item =>
                    item.FieldIdentity.ValueCategory == ValueCategory.AppraisedOriginalValue));
                Assert.AreEqual(3, occurrences.Count(item =>
                    item.FieldIdentity.ValueCategory == ValueCategory.AppraisedNetValue));
                Assert.AreEqual(2, occurrences.Count(item => item.FieldIdentity.TaxBasis == TaxBasis.TaxExcluded));
                Assert.AreEqual(2, occurrences.Count(item => item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded));
                Assert.AreEqual(2, occurrences.Count(item => item.FieldIdentity.TaxBasis == TaxBasis.Unspecified));
                Assert.IsTrue(occurrences.All(item =>
                    item.FieldIdentity.SectionCategory == SectionCategory.ValuationConclusion));
            }
        }

        private static SyntheticDocxFile CreateSyntheticAssessmentDocument()
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
                StyledParagraph("Heading2", "评估结论"),
                ParagraphWithText("本项目不含税评估原值为180万元，不含税评估净值为170万元。"),
                ParagraphWithText("含税评估原值为203.40万元，含税评估净值为192.10万元。"),
                ParagraphWithText("金额单位：万元"),
                new Table(
                    Row("序号", "资产名称", "数量", "评估原值", "评估净值"),
                    Row("1", "测试设备A", "10", "180.00", "170.00")));
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
