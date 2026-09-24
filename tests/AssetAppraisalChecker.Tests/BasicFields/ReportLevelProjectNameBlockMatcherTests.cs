using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class ReportLevelProjectNameBlockMatcherTests
    {
        private readonly ReportLevelProjectNameBlockMatcher _matcher =
            new ReportLevelProjectNameBlockMatcher();

        [TestMethod]
        public void FindMatches_Pattern1TwoParagraphDirect_AssemblesCompleteCandidate()
        {
            List<DocumentElement> elements = Paragraphs(
                "虚构集团拟处置",
                "设备资产公允价值",
                "资产评估报告");

            AssertCandidate(
                elements,
                "虚构集团拟处置设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Cover,
                ReportLevelProjectNameExtractionPattern.Pattern1TwoParagraphDirect,
                0,
                1,
                2);
        }

        [TestMethod]
        public void FindMatches_Pattern2ThreeParagraphBlankCover_AssemblesCompleteCandidate()
        {
            List<DocumentElement> elements = Paragraphs(
                "虚构集团拟接收",
                "用户设备资产涉及的",
                "三项资产公允价值",
                "   ",
                "资产评估报告");

            AssertCandidate(
                elements,
                "虚构集团拟接收用户设备资产涉及的三项资产公允价值",
                ReportLevelProjectNameAuthorityRole.Cover,
                ReportLevelProjectNameExtractionPattern
                    .Pattern2ThreeParagraphCoverWithBlankSeparator,
                0,
                2,
                4);
        }

        [TestMethod]
        public void FindMatches_Pattern3ThreeParagraphSummary_AssemblesCompleteCandidate()
        {
            List<DocumentElement> elements = Paragraphs(
                "虚构集团拟接收用户资产",
                "实施入账涉及的三项设备",
                "移交资产公允价值",
                "资产评估报告摘要");

            AssertCandidate(
                elements,
                "虚构集团拟接收用户资产实施入账涉及的三项设备移交资产公允价值",
                ReportLevelProjectNameAuthorityRole.Summary,
                ReportLevelProjectNameExtractionPattern.Pattern3ThreeParagraphSummaryDirect,
                0,
                2,
                3);
        }

        [TestMethod]
        public void FindMatches_Pattern4LeadingBreakBody_AssemblesCompleteCandidate()
        {
            List<DocumentElement> elements = Paragraphs(
                "\n虚构集团拟接收用户资产",
                "移交设备资产公允价值",
                "资产评估报告");
            elements[2] = Element(
                "资产评估报告",
                2,
                DocumentElementType.Heading,
                new[] { "资产评估报告" });

            AssertCandidate(
                elements,
                "虚构集团拟接收用户资产移交设备资产公允价值",
                ReportLevelProjectNameAuthorityRole.Body,
                ReportLevelProjectNameExtractionPattern
                    .Pattern4TwoParagraphBodyWithLeadingLineBreak,
                0,
                1,
                2);
        }

        [TestMethod]
        public void FindMatches_SingleNarrativeBeforeAnchor_ReturnsNothing()
        {
            AssertNoMatch(Paragraphs("本段说明虚构项目相关情况。", "资产评估报告"));
        }

        [TestMethod]
        public void FindMatches_TwoTitleParagraphsThenNarrativeBeforeAnchor_ReturnsNothing()
        {
            AssertNoMatch(Paragraphs(
                "虚构集团拟处置",
                "设备资产公允价值",
                "本段说明后续报告内容。",
                "资产评估报告"));
        }

        [TestMethod]
        public void FindMatches_ThreeParagraphsTwoBlanksBeforeCover_ReturnsNothing()
        {
            AssertNoMatch(Paragraphs(
                "虚构集团拟接收",
                "用户设备资产涉及的",
                "三项资产公允价值",
                string.Empty,
                "   ",
                "资产评估报告"));
        }

        [TestMethod]
        public void FindMatches_FourTitleParagraphsBeforeAnchor_ReturnsNothing()
        {
            AssertNoMatch(Paragraphs(
                "虚构集团拟",
                "开展设备资产",
                "处置业务涉及的",
                "资产公允价值",
                "资产评估报告摘要"));
        }

        [TestMethod]
        public void FindMatches_TableContentBeforeAnchor_ReturnsNothing()
        {
            var elements = new List<DocumentElement>
            {
                TableCell("虚构集团拟处置", 0, 0),
                TableCell("设备资产公允价值", 1, 1),
                Element("资产评估报告", 2)
            };

            AssertNoMatch(elements);
        }

        [TestMethod]
        public void FindMatches_NonAuthoritativeAnchor_ReturnsNothing()
        {
            AssertNoMatch(Paragraphs(
                "虚构集团拟处置",
                "设备资产公允价值",
                "资产评估业务说明"));
        }

        [TestMethod]
        public void FindMatches_OrdinaryBodyProjectNameReference_ReturnsNothing()
        {
            AssertNoMatch(Paragraphs(
                "虚构集团拟处置设备资产公允价值",
                "后续普通正文"));
        }

        [TestMethod]
        public void FindMatches_MoneyParagraphInBlock_ReturnsNothing()
        {
            AssertNoMatch(Paragraphs(
                "虚构集团拟处置设备资产",
                "人民币100万元",
                "资产评估报告"));
        }

        [TestMethod]
        public void FindMatches_ReportNumberParagraphInBlock_ReturnsNothing()
        {
            AssertNoMatch(Paragraphs(
                "虚构集团拟处置设备资产",
                "虚构评报字〔2026〕第001号",
                "资产评估报告"));
        }

        [TestMethod]
        public void FindMatches_IdenticalValuesWithoutAuthorityStructure_ReturnsNothing()
        {
            AssertNoMatch(Paragraphs(
                "虚构集团拟处置设备资产公允价值",
                "普通正文甲",
                "虚构集团拟处置设备资产公允价值",
                "普通正文乙"));
        }

        [TestMethod]
        public void Extract_BlockMatchedTitle_DoesNotKeepLegacyPartialOccurrence()
        {
            DocumentReadResult document = AsDocument(Paragraphs(
                "虚构集团拟处置",
                "设备资产公允价值评估项目",
                "资产评估报告摘要"));

            Assert.AreEqual(1, _matcher.FindMatches(document.Elements).Count);
            Assert.AreEqual(
                0,
                new BasicFieldExtractor().Extract(document).Count(item =>
                    item.FieldType == BasicFieldType.ProjectName));
        }

        [TestMethod]
        public void FindMatches_DifferentAuthoritativeBlocks_PreservesBothWithoutAutoEquivalence()
        {
            List<DocumentElement> elements = Paragraphs(
                "虚构集团拟接收",
                "用户设备资产公允价值",
                "资产评估报告",
                "虚构集团拟接收",
                "用户移交设备资产公允价值",
                "资产评估报告摘要");
            IReadOnlyList<ReportLevelProjectNameCandidate> candidates =
                _matcher.FindMatches(elements);

            Assert.AreEqual(2, candidates.Count);
            Assert.AreEqual(2, candidates.Select(item => item.CanonicalText).Distinct().Count());
            Assert.AreEqual(
                0,
                new BasicFieldExtractor().Extract(AsDocument(elements)).Count(item =>
                    item.FieldType == BasicFieldType.ProjectName));
        }

        private void AssertCandidate(
            IReadOnlyList<DocumentElement> elements,
            string expectedText,
            ReportLevelProjectNameAuthorityRole expectedRole,
            ReportLevelProjectNameExtractionPattern expectedPattern,
            int firstSourceIndex,
            int lastSourceIndex,
            int anchorIndex)
        {
            IReadOnlyList<ReportLevelProjectNameCandidate> matches =
                _matcher.FindMatches(elements);

            Assert.AreEqual(1, matches.Count);
            ReportLevelProjectNameCandidate candidate = matches[0];
            Assert.AreEqual(expectedText, candidate.AssembledText);
            Assert.AreEqual(expectedText, candidate.CanonicalText);
            Assert.IsTrue(candidate.NormalizationSuccess);
            Assert.AreEqual(expectedRole, candidate.AuthorityRole);
            Assert.AreEqual(expectedPattern, candidate.ExtractionPattern);
            Assert.AreEqual(lastSourceIndex - firstSourceIndex + 1, candidate.SourceParagraphs.Count);
            for (int index = firstSourceIndex; index <= lastSourceIndex; index++)
            {
                Assert.AreSame(
                    elements[index],
                    candidate.SourceParagraphs[index - firstSourceIndex]);
            }

            Assert.AreSame(elements[anchorIndex], candidate.Anchor);
        }

        private void AssertNoMatch(IEnumerable<DocumentElement> elements)
        {
            Assert.AreEqual(0, _matcher.FindMatches(elements).Count);
        }

        private static List<DocumentElement> Paragraphs(params string[] texts)
        {
            return texts
                .Select((text, index) => Element(text, index))
                .ToList();
        }

        private static DocumentReadResult AsDocument(IReadOnlyCollection<DocumentElement> elements)
        {
            return DocumentReadResult.Succeeded(
                elements,
                elements.Count(item => item.Location.ParagraphIndex.HasValue),
                elements.Any(item => item.Location.TableIndex.HasValue) ? 1 : 0);
        }

        private static DocumentElement Element(
            string text,
            int index,
            DocumentElementType elementType = DocumentElementType.Paragraph,
            IEnumerable<string> headingPath = null)
        {
            return new DocumentElement(
                elementType,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    index,
                    null,
                    null,
                    null,
                    headingPath),
                index,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }

        private static DocumentElement TableCell(string text, int order, int cellIndex)
        {
            return new DocumentElement(
                DocumentElementType.TableCell,
                text,
                new DocumentLocation(
                    DocumentPart.MainBody,
                    null,
                    0,
                    0,
                    cellIndex,
                    null),
                order,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
