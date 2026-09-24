using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class AuthoritativeAdjacentStandaloneProjectNameTests
    {
        private readonly BasicFieldExtractor _extractor = new BasicFieldExtractor();

        [TestMethod]
        public void Extract_NoHeadingOrSuffixBeforeCoverAnchor_ReturnsProjectName()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                Paragraphs(false, "虚构输电资产市场价值", "资产评估报告"));

            Assert.AreEqual("虚构输电资产市场价值", occurrence.RawValue);
            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("受限权威相邻结构")));
        }

        [TestMethod]
        public void Extract_NoSuffixBeforeSummaryAnchor_ReturnsProjectName()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                Paragraphs(true, "虚构配电资产市场价值", "资产评估报告摘要"));

            Assert.AreEqual("虚构配电资产市场价值", occurrence.RawValue);
            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("摘要式权威标题")));
        }

        [TestMethod]
        public void Extract_ExistingSuffixPath_RemainsSupported()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                Paragraphs(true, "虚构设备处置资产评估项目", "资产评估报告"));

            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("固定后缀")));
        }

        [TestMethod]
        public void Extract_ExistingHeadingScopedSummaryPath_RemainsSupported()
        {
            BasicFieldOccurrence occurrence = ExtractSingle(
                Paragraphs(true, "虚构车辆处置资产评估项目", "资产评估报告摘要"));

            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("固定后缀")));
        }

        [TestMethod]
        public void Extract_StandaloneOrdinaryTextWithoutAuthorityAnchor_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(false, "虚构普通业务说明文本", "普通业务标题"));
        }

        [TestMethod]
        public void Extract_NarrativeSentenceBeforeAuthorityAnchor_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(
                false,
                "本段说明虚构项目相关情况。",
                "资产评估报告"));
        }

        [TestMethod]
        public void Extract_NarrativeOpeningWithoutSentencePunctuation_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(
                false,
                "正文提及虚构输电资产市场价值",
                "资产评估报告"));
        }

        [TestMethod]
        public void Extract_SuffixFreeNumericCaptionBeforeAuthorityAnchor_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(
                false,
                "虚构甲线220千伏资产市场价值",
                "资产评估报告"));
        }

        [TestMethod]
        public void Extract_SuffixFreeEnumeratedCaptionBeforeAuthorityAnchor_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(
                false,
                "虚构设备甲、设备乙市场价值",
                "资产评估报告"));
        }

        [TestMethod]
        public void Extract_CompleteNarrativeReferenceWithoutAuthorityAnchor_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(
                false,
                "虚构公司设备资产评估项目",
                "后续普通正文"));
        }

        [TestMethod]
        public void Extract_OrdinaryElementBetweenCandidateAndAnchor_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(
                false,
                "虚构输电资产市场价值",
                "普通过渡正文",
                "资产评估报告"));
        }

        [TestMethod]
        public void Extract_BlankParagraphBeforeAuthorityAnchor_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(false, "   ", "资产评估报告"));
        }

        [TestMethod]
        [DataRow("短名称")]
        [DataRow("虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称虚构超长项目名称")]
        public void Extract_OutOfRangeCandidateLength_ReturnsNothing(string candidate)
        {
            AssertNoProjectName(Paragraphs(false, candidate, "资产评估报告"));
        }

        [TestMethod]
        public void Extract_CompetingImmediateAuthorityAnchors_ReturnsNothing()
        {
            var elements = new List<DocumentElement>
            {
                Element("虚构输电资产市场价值", 0, 0, false),
                Element("资产评估报告摘要", 1, 1, false),
                Element("资产评估报告", 2, 2, false)
            };

            AssertNoProjectName(DocumentReadResult.Succeeded(elements, 3, 0));
        }

        [TestMethod]
        public void Extract_DuplicateValuesWithoutAuthorityStructure_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(
                false,
                "虚构输电资产市场价值",
                "普通正文甲",
                "虚构输电资产市场价值",
                "普通正文乙"));
        }

        [TestMethod]
        public void Extract_OrdinaryHeadingIsNotAuthorityAnchor_ReturnsNothing()
        {
            AssertNoProjectName(Paragraphs(
                false,
                "虚构输电资产市场价值",
                "虚构评估业务说明"));
        }

        [TestMethod]
        public void Extract_UnrelatedTableDoesNotChangeAuthoritativeDecision()
        {
            IReadOnlyList<BasicFieldOccurrence> withoutTable = _extractor.Extract(
                Paragraphs(false, "虚构输电资产市场价值", "资产评估报告"));
            var withTable = new List<DocumentElement>
            {
                Element("虚构输电资产市场价值", 0, 0, false),
                Element("资产评估报告", 1, 1, false),
                new DocumentElement(
                    DocumentElementType.TableCell,
                    "虚构表格内容",
                    new DocumentLocation(
                        DocumentPart.MainBody,
                        null,
                        0,
                        0,
                        0,
                        null),
                    2,
                    string.Empty,
                    new DocumentNearbyContext(string.Empty, "虚构表格内容", string.Empty))
            };
            IReadOnlyList<BasicFieldOccurrence> withTableOccurrences = _extractor.Extract(
                DocumentReadResult.Succeeded(withTable, 2, 1));

            Assert.AreEqual(1, withoutTable.Count);
            Assert.AreEqual(1, withTableOccurrences.Count);
            Assert.AreEqual(
                withoutTable[0].NormalizedValue.CanonicalValue,
                withTableOccurrences[0].NormalizedValue.CanonicalValue);
        }

        private BasicFieldOccurrence ExtractSingle(DocumentReadResult document)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(document);
            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual(BasicFieldType.ProjectName, occurrences[0].FieldType);
            return occurrences[0];
        }

        private void AssertNoProjectName(DocumentReadResult document)
        {
            Assert.AreEqual(
                0,
                _extractor.Extract(document).Count(item =>
                    item.FieldType == BasicFieldType.ProjectName));
        }

        private static DocumentReadResult Paragraphs(
            bool withHeadingPath,
            params string[] texts)
        {
            var elements = new List<DocumentElement>();
            for (int index = 0; index < texts.Length; index++)
            {
                elements.Add(Element(texts[index], index, index, withHeadingPath));
            }

            return DocumentReadResult.Succeeded(elements, texts.Length, 0);
        }

        private static DocumentElement Element(
            string text,
            int documentOrderIndex,
            int paragraphIndex,
            bool withHeadingPath)
        {
            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    paragraphIndex,
                    null,
                    null,
                    null,
                    withHeadingPath ? new[] { "虚构报告结构" } : null),
                documentOrderIndex,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
