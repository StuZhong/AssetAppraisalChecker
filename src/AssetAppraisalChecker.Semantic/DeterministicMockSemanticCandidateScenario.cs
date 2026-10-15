using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class DeterministicMockSemanticCandidateScenario
    {
        public DeterministicMockSemanticCandidateScenario(
            string requestId,
            SemanticCandidateAnalysis analysis)
        {
            SemanticSourceLocation.RequireKey(requestId, nameof(requestId));
            Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
            if (!string.Equals(requestId, analysis.RequestId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Scenario RequestId 与 Analysis 不一致。",
                    nameof(analysis));
            }

            RequestId = requestId;
        }

        public string RequestId { get; }

        public SemanticCandidateAnalysis Analysis { get; }
    }
}
