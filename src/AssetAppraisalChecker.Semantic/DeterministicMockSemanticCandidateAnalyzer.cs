using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class DeterministicMockSemanticCandidateAnalyzer : ISemanticCandidateAnalyzer
    {
        private readonly IReadOnlyDictionary<string, SemanticCandidateAnalysis> _analyses;

        public DeterministicMockSemanticCandidateAnalyzer(
            IEnumerable<DeterministicMockSemanticCandidateScenario> scenarios)
        {
            if (scenarios == null)
            {
                throw new ArgumentNullException(nameof(scenarios));
            }

            List<DeterministicMockSemanticCandidateScenario> items = scenarios.ToList();
            if (items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Mock Scenario 不能包含 null。",
                    nameof(scenarios));
            }

            if (items.GroupBy(item => item.RequestId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Mock Scenario RequestId 不能重复。",
                    nameof(scenarios));
            }

            _analyses = items.ToDictionary(
                item => item.RequestId,
                item => item.Analysis,
                StringComparer.Ordinal);
        }

        public int CallCount { get; private set; }

        public SemanticCandidateAnalysis Analyze(SemanticCandidateRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            CallCount++;
            if (!_analyses.TryGetValue(request.RequestId, out SemanticCandidateAnalysis analysis))
            {
                return SemanticCandidateAnalysis.Abstained(
                    request.RequestId,
                    new SemanticCandidateSet(
                        request.CandidateSetIdentity,
                        new SemanticCandidate[0]),
                    SemanticCandidateFailureCode.UnsupportedText,
                    "mock.unsupported-text");
            }

            if (!analysis.CandidateSet.Identity.Equals(request.CandidateSetIdentity))
            {
                return SemanticCandidateAnalysis.Failed(
                    request.RequestId,
                    new SemanticCandidateSet(
                        request.CandidateSetIdentity,
                        new SemanticCandidate[0]),
                    SemanticCandidateFailureCode.InvalidOutput,
                    "mock.identity-mismatch");
            }

            if (analysis.CandidateSet.Candidates.Any(candidate =>
                !request.AllowedNormalizedCandidates.Contains(
                    candidate.NormalizedCandidate,
                    StringComparer.Ordinal)))
            {
                return SemanticCandidateAnalysis.Failed(
                    request.RequestId,
                    new SemanticCandidateSet(
                        request.CandidateSetIdentity,
                        new SemanticCandidate[0]),
                    SemanticCandidateFailureCode.InvalidOutput,
                    "mock.candidate-not-allowed");
            }

            return analysis;
        }
    }
}
