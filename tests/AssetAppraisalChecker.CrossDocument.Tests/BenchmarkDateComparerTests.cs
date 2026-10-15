using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.CrossDocument.Tests
{
    [TestClass]
    public sealed class BenchmarkDateComparerTests
    {
        [TestMethod]
        public void EqualStandardDates_ReturnPass()
        {
            ConsistencyResult result = new BenchmarkDateComparer().Compare(
                new[] { CreateFact("report", "2026年7月31日", "2026-07-31", 0) },
                new[] { CreateFact("explanation", "2026-07-31", "2026-07-31", 1) });

            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
            Assert.AreEqual(FactType.BenchmarkDate, result.FactType);
            StringAssert.Contains(result.Reason, "精确相等");
        }

        [TestMethod]
        public void DifferentStandardDates_ReturnFail()
        {
            ConsistencyResult result = new BenchmarkDateComparer().Compare(
                new[] { CreateFact("report", "2026年7月31日", "2026-07-31", 0) },
                new[] { CreateFact("explanation", "2026.08.01", "2026-08-01", 1) });

            Assert.AreEqual(ConsistencyStatus.FAIL, result.Status);
            StringAssert.Contains(result.Reason, "2026-07-31");
            StringAssert.Contains(result.Reason, "2026-08-01");
        }

        [TestMethod]
        public void MissingDateOnOneSide_ReturnsWarning()
        {
            ConsistencyResult result = new BenchmarkDateComparer().Compare(
                new[] { CreateFact("report", "2026/07/31", "2026-07-31", 0) },
                new BenchmarkDateFact[0]);

            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
            StringAssert.Contains(result.Reason, "缺少");
            Assert.IsTrue(
                ContainsEvidence(result.Evidence, "BenchmarkDateFactCount=0"));
        }

        [TestMethod]
        public void RepeatedEqualDates_AreComparedAndPreserveAllEvidence()
        {
            ConsistencyResult result = new BenchmarkDateComparer().Compare(
                new[]
                {
                    CreateFact("report", "2026年7月31日", "2026-07-31", 0),
                    CreateFact("report", "2026-07-31", "2026-07-31", 2)
                },
                new[] { CreateFact("explanation", "2026/07/31", "2026-07-31", 1) });

            Assert.AreEqual(ConsistencyStatus.PASS, result.Status);
            Assert.HasCount(3, result.Evidence);
            Assert.IsTrue(ContainsEvidence(result.Evidence, "2026年7月31日"));
            Assert.IsTrue(ContainsEvidence(result.Evidence, "2026/07/31"));
        }

        [TestMethod]
        public void MultipleDistinctDatesWithinDocument_ReturnFailWithoutGuessing()
        {
            ConsistencyResult result = new BenchmarkDateComparer().Compare(
                new[]
                {
                    CreateFact("report", "2026年7月31日", "2026-07-31", 0),
                    CreateFact("report", "2026年8月1日", "2026-08-01", 2)
                },
                new[] { CreateFact("explanation", "2026-07-31", "2026-07-31", 1) });

            Assert.AreEqual(ConsistencyStatus.FAIL, result.Status);
            StringAssert.Contains(result.Reason, "多个不同");
        }

        [TestMethod]
        public void FactWithoutStandardDate_ReturnsWarning()
        {
            var invalidFact = new BenchmarkDateFact(
                "report",
                CreateLocation(0),
                "评估基准日原文",
                CreateConfidence());

            ConsistencyResult result = new BenchmarkDateComparer().Compare(
                new[] { invalidFact },
                new[] { CreateFact("explanation", "2026-07-31", "2026-07-31", 1) });

            Assert.AreEqual(ConsistencyStatus.WARNING, result.Status);
            StringAssert.Contains(result.Reason, "缺少有效标准日期");
        }

        private static BenchmarkDateFact CreateFact(
            string sourceDocument,
            string originalText,
            string value,
            int paragraphIndex)
        {
            return new BenchmarkDateFact(
                sourceDocument,
                CreateLocation(paragraphIndex),
                originalText,
                CreateConfidence(),
                value);
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

        private static FactConfidence CreateConfidence()
        {
            return new FactConfidence(FactConfidenceBand.High, 0.95m);
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
