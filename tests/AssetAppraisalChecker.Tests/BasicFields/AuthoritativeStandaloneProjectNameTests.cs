using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class AuthoritativeStandaloneProjectNameTests
    {
        private readonly BasicFieldExtractor _extractor = new BasicFieldExtractor();
        private readonly BasicFieldConsistencyEngine _engine = new BasicFieldConsistencyEngine();

        [TestMethod]
        public void Extract_CoverStandaloneTitle_ReturnsProjectName()
        {
            BasicFieldOccurrence occurrence = ExtractSingleProjectName(
                "虚构设备处置资产评估项目",
                "资产评估报告");

            Assert.AreEqual("虚构设备处置资产评估项目", occurrence.RawValue);
            Assert.AreEqual(0, occurrence.TextStartIndex);
            Assert.AreEqual(occurrence.RawValue.Length, occurrence.TextLength);
        }

        [TestMethod]
        public void Extract_SummaryStandaloneTitle_ReturnsProjectName()
        {
            BasicFieldOccurrence occurrence = ExtractSingleProjectName(
                "虚构设备处置资产评估项目",
                "资产评估报告摘要");

            Assert.IsTrue(occurrence.Evidence.Any(item => item.Contains("摘要式权威标题")));
        }

        [TestMethod]
        public void Extract_StandaloneTitleWithoutAuthorityAnchor_ReturnsNothing()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                AuthoritativeParagraphs("虚构设备处置资产评估项目"));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_NarrativeParagraphEndingWithSuffix_ReturnsNothing()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                AuthoritativeParagraphs(
                    "本段说明涉及虚构设备处置资产评估项目。",
                    "资产评估报告"));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_ShortNarrativeReference_ReturnsNothing()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                AuthoritativeParagraphs(
                    "正文提及虚构设备处置资产",
                    "资产评估报告摘要"));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_AuthorityAnchorBeyondAdjacentElement_ReturnsNothing()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                AuthoritativeParagraphs(
                    "虚构设备处置资产评估项目",
                    string.Empty,
                    "资产评估报告"));

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_ProjectNameBoundary_ExcludesAuthorityAnchor()
        {
            BasicFieldOccurrence occurrence = ExtractSingleProjectName(
                "  虚构设备处置资产评估项目  ",
                "资产评估报告正文");

            Assert.AreEqual("虚构设备处置资产评估项目", occurrence.RawText);
            Assert.AreEqual("虚构设备处置资产评估项目", occurrence.RawValue);
            Assert.AreEqual(2, occurrence.TextStartIndex);
            Assert.AreEqual(occurrence.RawValue.Length, occurrence.TextLength);
            Assert.IsFalse(occurrence.RawValue.Contains("资产评估报告"));
        }

        [TestMethod]
        public void Extract_TwoIdenticalAuthoritativeTitles_ReturnsTwoWithoutConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                AuthoritativeParagraphs(
                    "虚构设备处置资产评估项目",
                    "资产评估报告摘要",
                    "虚构设备处置资产评估项目",
                    "资产评估报告正文"));

            Assert.AreEqual(2, occurrences.Count);
            BasicFieldConsistencyCheckResult result = _engine.Check(occurrences);
            Assert.AreEqual(1, result.ComparisonGroups.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void Extract_TwoDifferentAuthoritativeTitles_ReturnsConflict()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                AuthoritativeParagraphs(
                    "虚构设备甲处置资产评估项目",
                    "资产评估报告摘要",
                    "虚构设备乙处置资产评估项目",
                    "资产评估报告正文"));

            Assert.AreEqual(2, occurrences.Count);
            Assert.AreEqual(1, _engine.Check(occurrences).Issues.Count);
        }

        [TestMethod]
        public void Extract_ExistingExplicitLabelProjectName_RemainsSupported()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(
                    "项目名称：虚构设备处置资产评估项目"));

            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual("虚构设备处置资产评估项目", occurrences[0].RawValue);
        }

        [TestMethod]
        public void Extract_ExplicitLabelBesideAuthorityAnchor_DoesNotDuplicateLocation()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                BasicFieldTestFactory.Paragraphs(
                    "项目名称：虚构设备处置资产评估项目",
                    "资产评估报告"));

            Assert.AreEqual(1, occurrences.Count(item =>
                item.FieldType == BasicFieldType.ProjectName));
        }

        private BasicFieldOccurrence ExtractSingleProjectName(params string[] paragraphs)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = _extractor.Extract(
                AuthoritativeParagraphs(paragraphs));
            Assert.AreEqual(1, occurrences.Count);
            Assert.AreEqual(BasicFieldType.ProjectName, occurrences[0].FieldType);
            return occurrences[0];
        }

        private static DocumentReadResult AuthoritativeParagraphs(params string[] texts)
        {
            var elements = new List<DocumentElement>();
            for (int index = 0; index < texts.Length; index++)
            {
                string previous = index > 0 ? texts[index - 1] : string.Empty;
                string next = index + 1 < texts.Length ? texts[index + 1] : string.Empty;
                elements.Add(new DocumentElement(
                    string.Equals(texts[index], "资产评估报告正文")
                        ? DocumentElementType.Heading
                        : DocumentElementType.Paragraph,
                    texts[index],
                    new DocumentLocation(
                        DocumentPart.MainBody,
                        index,
                        null,
                        null,
                        null,
                        new[] { "虚构报告结构" }),
                    index,
                    string.Empty,
                    new DocumentNearbyContext(previous, texts[index], next)));
            }

            return DocumentReadResult.Succeeded(elements, texts.Length, 0);
        }
    }
}
