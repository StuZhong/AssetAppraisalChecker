using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.CrossDocument.Tests
{
    [TestClass]
    public sealed class ReportNumberComparerTests
    {
        [TestMethod]
        public void EqualNormalizedNumbers_ReturnPass()
        {
            ConsistencyResult result = new ReportNumberComparer().Compare(
                new[]
                {
                    CreateFact(
                        "report",
                        "云合成评报字（2026）第001号",
                        "云合成评报字〔2026〕第001号",
                        0)
                },
                new[]
                {
                    CreateFact(
                        "explanation",
                        "云合成评报字[2026]第001号",
                        "云合成评报字〔2026〕第001号",
                        1)
                });

            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
            Assert.AreEqual(FactType.ReportNumber, result.FactType);
            StringAssert.Contains(result.Reason, "精确相等");
            Assert.IsTrue(
                ContainsEvidence(result.Evidence, "ComparisonMode=OrdinalExact"));
        }

        [TestMethod]
        public void DifferentNormalizedNumbers_ReturnFail()
        {
            ConsistencyResult result = new ReportNumberComparer().Compare(
                OneFact("report", "第001号", "云合成评报字〔2026〕第001号", 0),
                OneFact("explanation", "第002号", "云合成评报字〔2026〕第002号", 1));

            Assert.AreEqual(ConsistencyStatus.FAIL, result.Status);
            StringAssert.Contains(result.Reason, "第001号");
            StringAssert.Contains(result.Reason, "第002号");
        }

        [TestMethod]
        public void MissingNumberOnOneSide_ReturnWarning()
        {
            ConsistencyResult result = new ReportNumberComparer().Compare(
                OneFact("report", "编号原文", "云合成评报字〔2026〕第001号", 0),
                new ReportNumberFact[0]);

            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
            StringAssert.Contains(result.Reason, "缺少");
            Assert.IsTrue(
                ContainsEvidence(result.Evidence, "ReportNumberFactCount=0"));
        }

        [TestMethod]
        public void RepeatedEqualNumbers_PassAndPreserveAllEvidence()
        {
            ConsistencyResult result = new ReportNumberComparer().Compare(
                new[]
                {
                    CreateFact(
                        "report",
                        "云合成评报字〔2026〕第001号",
                        "云合成评报字〔2026〕第001号",
                        0),
                    CreateFact(
                        "report",
                        "云合成评报字（2026）第001号",
                        "云合成评报字〔2026〕第001号",
                        2)
                },
                OneFact(
                    "explanation",
                    "云合成评报字[2026]第001号",
                    "云合成评报字〔2026〕第001号",
                    1));

            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
            Assert.IsTrue(result.Evidence.Count >= 4);
            Assert.IsTrue(
                ContainsEvidence(result.Evidence, "（2026）"));
            Assert.IsTrue(
                ContainsEvidence(result.Evidence, "[2026]"));
        }

        [TestMethod]
        public void MultipleDistinctNumbersWithinDocument_ReturnFailWithoutGuessing()
        {
            ConsistencyResult result = new ReportNumberComparer().Compare(
                new[]
                {
                    CreateFact(
                        "report",
                        "第001号",
                        "云合成评报字〔2026〕第001号",
                        0),
                    CreateFact(
                        "report",
                        "第002号",
                        "云合成评报字〔2026〕第002号",
                        2)
                },
                OneFact(
                    "explanation",
                    "第001号",
                    "云合成评报字〔2026〕第001号",
                    1));

            Assert.AreEqual(ConsistencyStatus.FAIL, result.Status);
            StringAssert.Contains(result.Reason, "多个不同");
        }

        [TestMethod]
        public void OrdinalDifferenceInLeadingZeroOrCase_ReturnFail()
        {
            ConsistencyResult leadingZero = new ReportNumberComparer().Compare(
                OneFact("report", "第001号", "云合成评报字〔2026〕第001号", 0),
                OneFact("explanation", "第1号", "云合成评报字〔2026〕第1号", 1));
            ConsistencyResult letterCase = new ReportNumberComparer().Compare(
                OneFact("report", "第A001号", "云合成评报字〔2026〕第A001号", 0),
                OneFact("explanation", "第a001号", "云合成评报字〔2026〕第a001号", 1));

            Assert.AreEqual(ConsistencyStatus.FAIL, leadingZero.Status);
            Assert.AreEqual(ConsistencyStatus.FAIL, letterCase.Status);
        }

        private static IReadOnlyList<ReportNumberFact> OneFact(
            string sourceDocument,
            string originalText,
            string normalizedNumber,
            int paragraphIndex)
        {
            return new[]
            {
                CreateFact(
                    sourceDocument,
                    originalText,
                    normalizedNumber,
                    paragraphIndex)
            };
        }

        private static ReportNumberFact CreateFact(
            string sourceDocument,
            string originalText,
            string normalizedNumber,
            int paragraphIndex)
        {
            return new ReportNumberFact(
                sourceDocument,
                CreateLocation(paragraphIndex),
                originalText,
                new FactConfidence(FactConfidenceBand.High, 0.95m),
                normalizedNumber);
        }

        private static DocumentLocation CreateLocation(int paragraphIndex)
        {
            return new DocumentLocation(
                DocumentPart.MainBody,
                paragraphIndex,
                null,
                null,
                null,
                new[] { "基本事项" });
        }

        private static bool ContainsEvidence(
            IReadOnlyList<string> evidence,
            string expected)
        {
            foreach (string item in evidence)
            {
                if (item.IndexOf(expected, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
