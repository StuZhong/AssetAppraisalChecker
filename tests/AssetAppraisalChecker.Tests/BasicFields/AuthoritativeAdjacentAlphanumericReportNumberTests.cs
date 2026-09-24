using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class AuthoritativeAdjacentAlphanumericReportNumberTests
    {
        private readonly BasicFieldExtractor _extractor = new BasicFieldExtractor();
        private readonly BasicFieldConsistencyEngine _engine = new BasicFieldConsistencyEngine();

        [TestMethod]
        public void Extract_OneLetterThreeDigitsAfterCoverAnchor_ReturnsReportNumber()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                Paragraphs("资产评估报告", "虚构评报字〔2026〕第A001号"));

            Assert.AreEqual("虚构评报字〔2026〕第A001号", occurrence.RawValue);
            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("受限字母数字序列")));
        }

        [TestMethod]
        public void Extract_OneLetterThreeDigitsAfterSummaryAnchor_ReturnsReportNumber()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                Paragraphs("资产评估报告摘要", "虚构评报字〔2026〕第B002号"));

            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("摘要式权威标题")));
        }

        [TestMethod]
        public void Extract_OneLetterThreeDigitsAfterBodyAnchor_ReturnsReportNumber()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                Paragraphs("资产评估报告正文", "虚构资评报字〔2026〕第C003号"));

            Assert.AreEqual(BasicFieldType.ReportNumber, occurrence.FieldType);
        }

        [TestMethod]
        public void Check_SameAuthoritativeAlphanumericNumberThreeTimes_ReturnsNoConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                Paragraphs(
                    "资产评估报告",
                    "虚构评报字〔2026〕第D004号",
                    "资产评估报告摘要",
                    "虚构评报字〔2026〕第D004号",
                    "资产评估报告正文",
                    "虚构评报字〔2026〕第D004号"));

            Assert.AreEqual(3, occurrences.Count);
            Assert.AreEqual(1, occurrences
                .Select(item => item.NormalizedValue.CanonicalValue)
                .Distinct()
                .Count());
            Assert.AreEqual(0, _engine.Check(occurrences).Issues.Count);
        }

        [TestMethod]
        public void Check_DifferentAuthoritativeAlphanumericNumber_ReturnsConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                Paragraphs(
                    "资产评估报告",
                    "虚构评报字〔2026〕第E005号",
                    "资产评估报告摘要",
                    "虚构评报字〔2026〕第F006号"));

            Assert.AreEqual(2, occurrences.Count);
            Assert.AreEqual(1, _engine.Check(occurrences).Issues.Count);
        }

        [TestMethod]
        public void Extract_AlphanumericNumberWithoutAuthorityAnchor_ReturnsNothing()
        {
            AssertNoReportNumber(Paragraphs("普通正文", "虚构评报字〔2026〕第G007号"));
        }

        [TestMethod]
        public void Extract_NarrativeContainingAlphanumericNumber_ReturnsNothing()
        {
            AssertNoReportNumber(Paragraphs(
                "资产评估报告",
                "正文引用虚构评报字〔2026〕第H008号进行说明。"));
        }

        [TestMethod]
        public void Extract_RegulatoryDocumentWithSameSequence_ReturnsNothing()
        {
            AssertNoReportNumber(Paragraphs(
                "资产评估报告",
                "虚构管理办法〔2026〕第J009号"));
        }

        [TestMethod]
        public void Extract_OtherDocumentReferenceWithSameSequence_ReturnsNothing()
        {
            AssertNoReportNumber(Paragraphs(
                "普通正文",
                "虚构文件评报字〔2026〕第K010号"));
        }

        [TestMethod]
        public void Extract_AlphanumericNumberAfterOrdinaryHeading_ReturnsNothing()
        {
            AssertNoReportNumber(Paragraphs(
                "虚构评估业务说明",
                "虚构评报字〔2026〕第L011号"));
        }

        [TestMethod]
        public void Extract_OrdinaryElementBetweenAnchorAndNumber_ReturnsNothing()
        {
            AssertNoReportNumber(Paragraphs(
                "资产评估报告",
                "普通过渡正文",
                "虚构评报字〔2026〕第M012号"));
        }

        [TestMethod]
        public void Extract_RepeatedAlphanumericValuesWithoutAuthorityRemainExcluded()
        {
            AssertNoReportNumber(Paragraphs(
                "普通正文甲",
                "虚构评报字〔2026〕第N013号",
                "普通正文乙",
                "虚构评报字〔2026〕第N013号"));
        }

        [TestMethod]
        [DataRow("虚构评报字〔2026〕第AB123号")]
        [DataRow("虚构评报字〔2026〕第A12号")]
        [DataRow("虚构评报字〔2026〕第A1234号")]
        [DataRow("虚构评报字〔2026〕第A-123号")]
        [DataRow("虚构评报字〔2026〕第A 123号")]
        [DataRow("虚构评报字〔2026〕第ABCD号")]
        [DataRow("虚构评报字〔2026〕第A123B456C789号")]
        public void Extract_UnsupportedAlphanumericSequence_ReturnsNothing(string number)
        {
            AssertNoReportNumber(Paragraphs("资产评估报告", number));
        }

        [TestMethod]
        public void Extract_ExistingNumericStandaloneNumberStillWorksWithoutAnchor()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                Paragraphs("虚构评报字〔2026〕第001号"));

            Assert.AreEqual("虚构评报字〔2026〕第001号", occurrence.RawValue);
        }

        [TestMethod]
        public void Extract_CompetingPreviousElementsFailClosed()
        {
            var elements = new List<DocumentElement>
            {
                Element(DocumentElementType.Paragraph, "资产评估报告", 0, 0),
                Element(DocumentElementType.Heading, "资产评估报告摘要", 0, 1),
                Element(DocumentElementType.Paragraph, "虚构评报字〔2026〕第P014号", 1, 2)
            };

            AssertNoReportNumber(DocumentReadResult.Succeeded(elements, 3, 0));
        }

        [TestMethod]
        public void Extract_AlphanumericNumberInTableCellReturnsNothing()
        {
            var elements = new List<DocumentElement>
            {
                Element(DocumentElementType.Paragraph, "资产评估报告", 0, 0),
                new DocumentElement(
                    DocumentElementType.TableCell,
                    "虚构评报字〔2026〕第Q015号",
                    new DocumentLocation(
                        DocumentPart.MainBody,
                        null,
                        0,
                        0,
                        0,
                        null),
                    1,
                    string.Empty,
                    new DocumentNearbyContext(
                        "资产评估报告",
                        "虚构评报字〔2026〕第Q015号",
                        string.Empty))
            };

            AssertNoReportNumber(DocumentReadResult.Succeeded(elements, 1, 1));
        }

        private BasicFieldOccurrence ExtractSingle(DocumentReadResult document)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(document);
            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual(BasicFieldType.ReportNumber, occurrences[0].FieldType);
            return occurrences[0];
        }

        private void AssertNoReportNumber(DocumentReadResult document)
        {
            Assert.AreEqual(
                0,
                _extractor.Extract(document).Count(item =>
                    item.FieldType == BasicFieldType.ReportNumber));
        }

        private static DocumentReadResult Paragraphs(params string[] texts)
        {
            var elements = new List<DocumentElement>();
            for (int index = 0; index < texts.Length; index++)
            {
                elements.Add(Element(
                    DocumentElementType.Paragraph,
                    texts[index],
                    index,
                    index));
            }

            return DocumentReadResult.Succeeded(elements, texts.Length, 0);
        }

        private static DocumentElement Element(
            DocumentElementType elementType,
            string text,
            int documentOrderIndex,
            int paragraphIndex)
        {
            return new DocumentElement(
                elementType,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    paragraphIndex,
                    null,
                    null,
                    null,
                    null),
                documentOrderIndex,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
