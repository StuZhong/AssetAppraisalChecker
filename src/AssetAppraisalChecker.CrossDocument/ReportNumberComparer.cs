using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.CrossDocument
{
    public sealed class ReportNumberComparer
    {
        public ConsistencyResult Compare(
            IReadOnlyList<ReportNumberFact> firstFacts,
            IReadOnlyList<ReportNumberFact> secondFacts)
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
                    "至少一个文档缺少可比较的 ReportNumberFact，无法判断报告编号一致性。",
                    evidence);
            }

            if (firstFacts.Any(fact =>
                    string.IsNullOrWhiteSpace(fact.NormalizedNumber)) ||
                secondFacts.Any(fact =>
                    string.IsNullOrWhiteSpace(fact.NormalizedNumber)))
            {
                return CreateResult(
                    ConsistencyStatus.WARNING,
                    "至少一个 ReportNumberFact 缺少有效标准编号，未使用原文进行猜测性比较。",
                    evidence);
            }

            List<string> firstNumbers = GetDistinctNumbers(firstFacts);
            List<string> secondNumbers = GetDistinctNumbers(secondFacts);

            if (firstNumbers.Count > 1 || secondNumbers.Count > 1)
            {
                return CreateResult(
                    ConsistencyStatus.FAIL,
                    "至少一个文档包含多个不同的明确报告编号，存在确定性编号冲突。",
                    evidence);
            }

            string firstNumber = firstNumbers[0];
            string secondNumber = secondNumbers[0];
            if (string.Equals(
                    firstNumber,
                    secondNumber,
                    StringComparison.Ordinal))
            {
                return CreateResult(
                    ConsistencyStatus.PASS,
                    "两份文档的报告编号标准化后精确相等。",
                    evidence);
            }

            return CreateResult(
                ConsistencyStatus.FAIL,
                string.Concat(
                    "两份文档的报告编号标准值明确不同：",
                    firstNumber,
                    " vs ",
                    secondNumber,
                    "。"),
                evidence);
        }

        private static List<string> GetDistinctNumbers(
            IEnumerable<ReportNumberFact> facts)
        {
            return facts
                .Select(fact => fact.NormalizedNumber)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static IReadOnlyList<string> BuildEvidence(
            IReadOnlyList<ReportNumberFact> firstFacts,
            IReadOnlyList<ReportNumberFact> secondFacts)
        {
            var evidence = new List<string>();
            AddEvidence(evidence, firstFacts, "FirstDocument");
            AddEvidence(evidence, secondFacts, "SecondDocument");
            evidence.Add("ComparisonMode=OrdinalExact");
            return evidence;
        }

        private static void AddEvidence(
            ICollection<string> evidence,
            IReadOnlyList<ReportNumberFact> facts,
            string documentRole)
        {
            if (facts.Count == 0)
            {
                evidence.Add(string.Concat(
                    documentRole,
                    " | ReportNumberFactCount=0"));
                return;
            }

            foreach (ReportNumberFact fact in facts)
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
                FactType.ReportNumber,
                status,
                reason,
                evidence);
        }

        private static void ValidateFacts(
            IReadOnlyList<ReportNumberFact> facts,
            string parameterName)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (facts.Any(fact => fact == null))
            {
                throw new ArgumentException(
                    "ReportNumberFact 集合不能包含 null。",
                    parameterName);
            }
        }
    }
}
