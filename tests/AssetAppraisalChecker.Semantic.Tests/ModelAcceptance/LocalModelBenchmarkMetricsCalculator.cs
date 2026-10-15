using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal sealed class LocalModelBenchmarkMetricsCalculator
    {
        internal LocalModelBenchmarkMetrics Calculate(
            IEnumerable<LocalSemanticAcceptanceCase> cases,
            IEnumerable<LocalModelBenchmarkCaseResult> results)
        {
            if (cases == null || results == null)
            {
                throw new ArgumentNullException(
                    cases == null ? nameof(cases) : nameof(results));
            }

            LocalSemanticAcceptanceCase[] caseItems = cases.ToArray();
            LocalModelBenchmarkCaseResult[] resultItems = results.ToArray();
            Dictionary<string, LocalModelBenchmarkCaseResult> byId = resultItems
                .ToDictionary(item => item.CaseId, StringComparer.Ordinal);
            if (caseItems.Length != resultItems.Length
                || caseItems.Any(item => !byId.ContainsKey(item.CaseId)))
            {
                throw new InvalidOperationException(
                    "Metrics require one result for every dataset case.");
            }

            int expectedCandidateCount = caseItems.Count(
                item => !string.IsNullOrWhiteSpace(item.ExpectedCandidate));
            int correctCandidateCount = caseItems.Count(item =>
                !string.IsNullOrWhiteSpace(item.ExpectedCandidate)
                && string.Equals(
                    byId[item.CaseId].ActualCandidate,
                    item.ExpectedCandidate,
                    StringComparison.Ordinal));

            int truePositive = caseItems.Count(item => IsCorrectAcceptance(
                item,
                byId[item.CaseId]));
            int falseNegative = caseItems.Count(item =>
                IsExpectedAcceptance(item) && !IsCorrectAcceptance(item, byId[item.CaseId]));
            int falsePositive = caseItems.Count(item =>
                IsAccepted(byId[item.CaseId]) && !IsCorrectAcceptance(item, byId[item.CaseId]));
            int trueNegative = caseItems.Count(item =>
                !IsExpectedAcceptance(item) && !IsAccepted(byId[item.CaseId]));
            int warningCount = resultItems.Count(item =>
                item.Decision.Decision == SemanticCandidateAcceptanceDecisionKind.Warning);
            LocalSemanticAcceptanceCase[] hardNegatives = caseItems
                .Where(item => item.IsHardNegative)
                .ToArray();
            int acceptedHardNegatives = hardNegatives.Count(
                item => IsAccepted(byId[item.CaseId]));
            decimal[] latencies = resultItems
                .Select(item => item.LatencyMilliseconds)
                .OrderBy(value => value)
                .ToArray();
            decimal[] margins = resultItems
                .Where(item => item.Top1Top2Margin.HasValue)
                .Select(item => item.Top1Top2Margin.Value)
                .ToArray();

            return new LocalModelBenchmarkMetrics
            {
                TotalCases = caseItems.Length,
                CandidateAccuracy = Ratio(correctCandidateCount, expectedCandidateCount),
                Precision = Ratio(truePositive, truePositive + falsePositive),
                Recall = Ratio(truePositive, truePositive + falseNegative),
                FalsePositiveRate = Ratio(falsePositive, falsePositive + trueNegative),
                FalseNegativeRate = Ratio(falseNegative, truePositive + falseNegative),
                AbstentionRate = Ratio(warningCount, caseItems.Length),
                HardNegativeAcceptanceRate = Ratio(
                    acceptedHardNegatives,
                    hardNegatives.Length),
                AverageTop1Top2Margin = margins.Length == 0
                    ? (decimal?)null
                    : margins.Average(),
                AverageLatencyMilliseconds = latencies.Average(),
                P50LatencyMilliseconds = Percentile(latencies, 0.50m),
                P95LatencyMilliseconds = Percentile(latencies, 0.95m),
                PeakMemoryBytes = null,
                ModelLoadTimeMilliseconds = null
            };
        }

        private static bool IsExpectedAcceptance(LocalSemanticAcceptanceCase item)
        {
            return string.Equals(
                item.ExpectedDecisionClass,
                "AcceptedCandidate",
                StringComparison.Ordinal);
        }

        private static bool IsAccepted(LocalModelBenchmarkCaseResult item)
        {
            return item.Decision.Decision
                == SemanticCandidateAcceptanceDecisionKind.AcceptedCandidate;
        }

        private static bool IsCorrectAcceptance(
            LocalSemanticAcceptanceCase expected,
            LocalModelBenchmarkCaseResult actual)
        {
            return IsExpectedAcceptance(expected)
                && IsAccepted(actual)
                && string.Equals(
                    expected.ExpectedCandidate,
                    actual.ActualCandidate,
                    StringComparison.Ordinal);
        }

        private static decimal Ratio(int numerator, int denominator)
        {
            return denominator == 0 ? 0m : (decimal)numerator / denominator;
        }

        private static decimal Percentile(decimal[] sortedValues, decimal percentile)
        {
            int index = (int)Math.Ceiling(sortedValues.Length * percentile) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Length - 1))];
        }
    }
}
