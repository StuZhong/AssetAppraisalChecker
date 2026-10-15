using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.CrossDocument
{
    public sealed class BenchmarkDateComparer
    {
        public ConsistencyResult Compare(
            IReadOnlyList<BenchmarkDateFact> firstFacts,
            IReadOnlyList<BenchmarkDateFact> secondFacts)
        {
            ValidateFacts(firstFacts, nameof(firstFacts));
            ValidateFacts(secondFacts, nameof(secondFacts));

            IReadOnlyList<string> evidence = BuildEvidence(
                firstFacts,
                secondFacts);

            if (firstFacts.Count == 0 || secondFacts.Count == 0)
            {
                return CreateResult(
                    ConsistencyStatus.WARNING,
                    "至少一个文档缺少可比较的 BenchmarkDateFact，无法判断评估基准日一致性。",
                    evidence);
            }

            if (firstFacts.Any(fact => !fact.NormalizedDate.HasValue) ||
                secondFacts.Any(fact => !fact.NormalizedDate.HasValue))
            {
                return CreateResult(
                    ConsistencyStatus.WARNING,
                    "至少一个 BenchmarkDateFact 缺少有效标准日期，未使用原文进行猜测性比较。",
                    evidence);
            }

            List<DateTime> firstDates = GetDistinctDates(firstFacts);
            List<DateTime> secondDates = GetDistinctDates(secondFacts);

            if (firstDates.Count > 1 || secondDates.Count > 1)
            {
                return CreateResult(
                    ConsistencyStatus.FAIL,
                    "至少一个文档包含多个不同的明确评估基准日，存在确定性日期冲突。",
                    evidence);
            }

            DateTime firstDate = firstDates[0];
            DateTime secondDate = secondDates[0];
            if (firstDate == secondDate)
            {
                return CreateResult(
                    ConsistencyStatus.PASS,
                    "两份文档的评估基准日标准日期精确相等。",
                    evidence);
            }

            return CreateResult(
                ConsistencyStatus.FAIL,
                string.Concat(
                    "两份文档的评估基准日标准日期明确不同：",
                    firstDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    " vs ",
                    secondDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    "。"),
                evidence);
        }

        private static List<DateTime> GetDistinctDates(
            IEnumerable<BenchmarkDateFact> facts)
        {
            return facts
                .Select(fact => fact.NormalizedDate.Value.Date)
                .Distinct()
                .OrderBy(date => date)
                .ToList();
        }

        private static IReadOnlyList<string> BuildEvidence(
            IReadOnlyList<BenchmarkDateFact> firstFacts,
            IReadOnlyList<BenchmarkDateFact> secondFacts)
        {
            var evidence = new List<string>();
            AddEvidence(evidence, firstFacts, "FirstDocument");
            AddEvidence(evidence, secondFacts, "SecondDocument");
            return evidence;
        }

        private static void AddEvidence(
            ICollection<string> evidence,
            IReadOnlyList<BenchmarkDateFact> facts,
            string documentRole)
        {
            if (facts.Count == 0)
            {
                evidence.Add(string.Concat(
                    documentRole,
                    " | BenchmarkDateFactCount=0"));
                return;
            }

            foreach (BenchmarkDateFact fact in facts)
            {
                foreach (string factEvidence in fact.Evidence)
                {
                    evidence.Add(string.Concat(
                        documentRole,
                        " | ",
                        factEvidence));
                }
            }
        }

        private static ConsistencyResult CreateResult(
            ConsistencyStatus status,
            string reason,
            IEnumerable<string> evidence)
        {
            return new ConsistencyResult(
                FactType.BenchmarkDate,
                status,
                reason,
                evidence);
        }

        private static void ValidateFacts(
            IReadOnlyList<BenchmarkDateFact> facts,
            string parameterName)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (facts.Any(fact => fact == null))
            {
                throw new ArgumentException(
                    "BenchmarkDateFact 集合不能包含 null。",
                    parameterName);
            }
        }
    }
}
