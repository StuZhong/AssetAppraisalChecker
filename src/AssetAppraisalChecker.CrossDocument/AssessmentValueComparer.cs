using System;
using System.Collections.Generic;
using System.Globalization;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.CrossDocument
{
    public sealed class AssessmentValueComparer
    {
        public ConsistencyResult Compare(
            AssessmentValueFact firstFact,
            AssessmentValueFact secondFact)
        {
            IReadOnlyList<string> evidence = BuildEvidence(
                firstFact,
                secondFact);

            if (firstFact == null || secondFact == null)
            {
                return new ConsistencyResult(
                    FactType.AssessmentValue,
                    ConsistencyStatus.WARNING,
                    "至少一个文档缺少可比较的 AssessmentValueFact，无法判断金额一致性。",
                    evidence);
            }

            if (!firstFact.NormalizedAmountYuan.HasValue ||
                !secondFact.NormalizedAmountYuan.HasValue)
            {
                return new ConsistencyResult(
                    FactType.AssessmentValue,
                    ConsistencyStatus.WARNING,
                    "至少一个 AssessmentValueFact 缺少标准化 decimal 元值，无法判断金额一致性。",
                    evidence);
            }

            decimal firstAmount = firstFact.NormalizedAmountYuan.Value;
            decimal secondAmount = secondFact.NormalizedAmountYuan.Value;
            if (firstAmount == secondAmount)
            {
                return new ConsistencyResult(
                    FactType.AssessmentValue,
                    ConsistencyStatus.PASS,
                    "两份文档的评估价值标准化金额精确相等。",
                    evidence);
            }

            decimal difference = Math.Abs(firstAmount - secondAmount);
            return new ConsistencyResult(
                FactType.AssessmentValue,
                ConsistencyStatus.FAIL,
                string.Concat(
                    "两份文档的评估价值标准化金额明确不同，差额为",
                    FormatAmount(difference),
                    "元。"),
                evidence);
        }

        private static IReadOnlyList<string> BuildEvidence(
            AssessmentValueFact firstFact,
            AssessmentValueFact secondFact)
        {
            var evidence = new List<string>();
            AddEvidence(evidence, firstFact);
            AddEvidence(evidence, secondFact);

            if (evidence.Count == 0)
            {
                evidence.Add("AssessmentValue | 两份文档均未提取到评估价值事实。");
            }

            return evidence;
        }

        private static void AddEvidence(
            ICollection<string> evidence,
            AssessmentValueFact fact)
        {
            if (fact == null)
            {
                return;
            }

            evidence.Add(string.Concat(
                fact.SourceDocument,
                " | ",
                fact.OriginalText,
                " | NormalizedAmountYuan=",
                fact.NormalizedAmountYuan.HasValue
                    ? FormatAmount(fact.NormalizedAmountYuan.Value)
                    : "<missing>"));
        }

        private static string FormatAmount(decimal amount)
        {
            return amount.ToString(
                "0.############################",
                CultureInfo.InvariantCulture);
        }
    }
}
