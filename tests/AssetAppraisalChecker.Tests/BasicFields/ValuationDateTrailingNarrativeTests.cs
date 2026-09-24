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
    public sealed class ValuationDateTrailingNarrativeTests
    {
        private readonly BasicFieldExtractor extractor = new BasicFieldExtractor();

        [TestMethod]
        public void Extract_SingleDateWithTrailingNarrative_UsesOnlyDateToken()
        {
            const string text = "评估基准日：2036年4月5日 后续为完全虚构的说明文字";

            BasicFieldOccurrence occurrence = Extract(text).Single();

            AssertSuccessfulDate(occurrence, text, "2036年4月5日", "2036-04-05");
        }

        [TestMethod]
        public void Extract_SingleDateWithCommaNarrative_UsesOnlyDateToken()
        {
            const string text = "评估基准日：2036年4月5日，后续为完全虚构的说明文字";

            BasicFieldOccurrence occurrence = Extract(text).Single();

            AssertSuccessfulDate(occurrence, text, "2036年4月5日", "2036-04-05");
        }

        [TestMethod]
        public void Extract_SingleDateWithSentenceNarrative_UsesOnlyDateToken()
        {
            const string text = "评估基准日：2036年4月5日。后续为完全虚构的说明文字";

            BasicFieldOccurrence occurrence = Extract(text).Single();

            AssertSuccessfulDate(occurrence, text, "2036年4月5日", "2036-04-05");
        }

        [TestMethod]
        public void Check_FailedWideCandidateAndPreciseCandidateOverlap_PrefersSuccess()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2036年4月5日 后续为完全虚构的说明文字");
            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(1, occurrences.Count);
            Assert.IsTrue(occurrences[0].HasNormalizedValue);
            Assert.IsFalse(result.Issues.Any(item =>
                item.IssueType == IssueType.BasicFieldParseFailed));
        }

        [TestMethod]
        public void Extract_TrailingNarrative_DoesNotCreateDuplicateOccurrence()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2036年4月5日，后续为完全虚构的说明文字");

            Assert.AreEqual(1, occurrences.Count(item =>
                item.FieldType == BasicFieldType.ValuationDate));
        }

        [TestMethod]
        public void Extract_TwoDates_DoesNotUseSingleDateBoundaryRule()
        {
            AssertFailedOccurrence(
                "评估基准日：2036年4月5日，另一个日期为2036年4月6日");
        }

        [TestMethod]
        public void Extract_AuditedRange_ContinuesToUseExistingRangeRule()
        {
            const string text = "评估基准日2036年4月5日起至2036年4月8日";

            BasicFieldOccurrence occurrence = Extract(text).Single();

            AssertSuccessfulDate(occurrence, text, "2036年4月5日", "2036-04-05");
        }

        [TestMethod]
        public void Extract_ZhiToken_DoesNotUseTrailingNarrativeRule()
        {
            AssertFailedOccurrence("评估基准日：2036年4月5日至日期待定");
        }

        [TestMethod]
        public void Extract_QiZhiTokenWithoutSecondDate_DoesNotUseTrailingNarrativeRule()
        {
            AssertFailedOccurrence("评估基准日：2036年4月5日起至日期待定");
        }

        [TestMethod]
        public void Extract_JieZhiToken_DoesNotUseTrailingNarrativeRule()
        {
            AssertFailedOccurrence("评估基准日：2036年4月5日，截止事项另行说明");
        }

        [TestMethod]
        public void Extract_QiJianToken_DoesNotUseTrailingNarrativeRule()
        {
            AssertFailedOccurrence("评估基准日：2036年4月5日，期间事项另行说明");
        }

        [TestMethod]
        public void Extract_IncompleteDate_IsNotCompleted()
        {
            AssertFailedOccurrence("评估基准日：2036年4月 后续为完全虚构说明");
        }

        [TestMethod]
        public void Extract_InvalidDate_IsNotAcceptedByTrailingNarrativeRule()
        {
            AssertFailedOccurrence("评估基准日：2036年2月30日 后续为完全虚构说明");
        }

        [TestMethod]
        public void Extract_WithoutValuationDateLabel_DoesNotTrigger()
        {
            Assert.AreEqual(0, Extract("日期：2036年4月5日 后续为完全虚构说明").Count);
        }

        [TestMethod]
        public void Extract_IncidentalNarrativeDate_DoesNotTrigger()
        {
            Assert.AreEqual(0, Extract("完全虚构事项发生于2036年4月5日，后续另行说明").Count);
        }

        [TestMethod]
        public void Extract_DateAcrossParagraph_DoesNotTrigger()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：",
                "2036年4月5日 后续为完全虚构说明");

            Assert.AreEqual(0, occurrences.Count);
        }

        [TestMethod]
        public void Extract_TwoIndependentOccurrences_AreBothRetained()
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(
                "评估基准日：2036年4月5日 后续为第一处完全虚构说明",
                "评估基准日：2036年4月6日 后续为第二处完全虚构说明");
            BasicFieldConsistencyCheckResult result =
                new BasicFieldConsistencyEngine().Check(occurrences);

            Assert.AreEqual(2, occurrences.Count);
            Assert.IsTrue(occurrences.All(item => item.HasNormalizedValue));
            Assert.AreEqual(1, result.Issues.Count(item =>
                item.IssueType == IssueType.BasicFieldConflict));
        }

        [TestMethod]
        public void Extract_ExistingBridge_RemainsSupported()
        {
            BasicFieldOccurrence occurrence = Extract("评估基准日是2036年4月5日").Single();

            AssertSuccessfulDate(
                occurrence,
                "评估基准日是2036年4月5日",
                "2036年4月5日",
                "2036-04-05");
        }

        private IReadOnlyList<BasicFieldOccurrence> Extract(params string[] paragraphs)
        {
            return extractor.Extract(BasicFieldTestFactory.Paragraphs(paragraphs));
        }

        private void AssertFailedOccurrence(string text)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences = Extract(text);

            Assert.AreEqual(1, occurrences.Count, text);
            Assert.AreEqual(BasicFieldType.ValuationDate, occurrences[0].FieldType);
            Assert.AreEqual(BasicFieldParseStatus.Failed, occurrences[0].ParseStatus);
            Assert.IsFalse(occurrences[0].HasNormalizedValue);
        }

        private static void AssertSuccessfulDate(
            BasicFieldOccurrence occurrence,
            string sourceText,
            string expectedRawDate,
            string expectedCanonicalDate)
        {
            int expectedStart = sourceText.IndexOf(
                expectedRawDate,
                System.StringComparison.Ordinal);
            Assert.AreEqual(BasicFieldType.ValuationDate, occurrence.FieldType);
            Assert.AreEqual(BasicFieldParseStatus.Success, occurrence.ParseStatus);
            Assert.AreEqual(expectedRawDate, occurrence.RawValue);
            Assert.AreEqual(expectedRawDate, occurrence.RawText);
            Assert.AreEqual(expectedCanonicalDate, occurrence.NormalizedValue.CanonicalValue);
            Assert.AreEqual(expectedStart, occurrence.TextStartIndex);
            Assert.AreEqual(expectedRawDate.Length, occurrence.TextLength);
        }
    }
}
