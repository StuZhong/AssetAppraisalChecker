using System.Collections.Generic;

namespace AssetAppraisalChecker.Semantic.Tests
{
    internal static class SemanticCandidateTestData
    {
        internal const string ModelId = "mock-evaluation-method";
        internal const string ModelVersion = "1.0.0";
        internal const string AnalyzerVersion = "candidate-mock/1.0";
        internal const string SubjectKey = "subject/v1:building";
        internal const string LocationKey = "loc/v1:report:body:p1";

        internal static readonly string ModelHash =
            SemanticHash.ComputeSha256("mock-model-v1");

        internal static SemanticSourceLocation Location(
            string sourceDocument = "report",
            string locationKey = LocationKey)
        {
            return new SemanticSourceLocation(
                sourceDocument,
                locationKey,
                "正文第1段");
        }

        internal static SemanticCandidateEvidence Evidence(
            string originalText = "房屋建筑物按现行重建成本测算。",
            string sourceDocument = "report",
            string locationKey = LocationKey)
        {
            return new SemanticCandidateEvidence(
                Location(sourceDocument, locationKey),
                originalText,
                "CandidatePhrase",
                SemanticHash.ComputeSha256(originalText));
        }

        internal static SemanticCandidateSetIdentity SetIdentity(
            SemanticCandidateType type = SemanticCandidateType.EvaluationMethod,
            string subjectKey = SubjectKey,
            string locationKey = LocationKey,
            SemanticCandidateRole role = SemanticCandidateRole.AdoptedMethod)
        {
            return new SemanticCandidateSetIdentity(
                type,
                subjectKey,
                locationKey,
                role);
        }

        internal static SemanticCandidate Candidate(
            string normalizedCandidate = "method/v1:cost-approach",
            string candidateValue = "成本法",
            decimal modelConfidence = 0.95m,
            decimal? similarityScore = 0.93m,
            SemanticCandidateSetIdentity setIdentity = null,
            string originalText = "房屋建筑物按现行重建成本测算。")
        {
            SemanticCandidateSetIdentity identity = setIdentity ?? SetIdentity();
            SemanticSourceLocation location = Location(
                "report",
                identity.SourceLocationKey);
            SemanticCandidateEvidence evidence = new SemanticCandidateEvidence(
                location,
                originalText,
                "CandidatePhrase",
                SemanticHash.ComputeSha256(originalText));
            return new SemanticCandidate(
                new SemanticCandidateIdentity(identity, normalizedCandidate),
                candidateValue,
                normalizedCandidate,
                similarityScore,
                modelConfidence,
                location,
                new[] { evidence },
                ModelId,
                ModelVersion,
                AnalyzerVersion,
                ModelHash,
                "受控 Mock 候选",
                SemanticCandidateCreatedBy.DeterministicMock);
        }

        internal static SemanticCandidateAnalysis Analysis(
            string requestId,
            SemanticCandidateSetIdentity identity,
            params SemanticCandidate[] candidates)
        {
            return SemanticCandidateAnalysis.Succeeded(
                requestId,
                new SemanticCandidateSet(identity, candidates));
        }

        internal static SemanticCandidateRequest Request(
            string requestId,
            SemanticCandidateSetIdentity identity,
            IEnumerable<string> allowedCandidates)
        {
            return new SemanticCandidateRequest(
                requestId,
                identity,
                new[] { Evidence(locationKey: identity.SourceLocationKey) },
                allowedCandidates,
                "evaluation-method/v1",
                "candidate-mock-policy/v1",
                true);
        }

        internal static SystemAcceptanceThreshold Threshold(
            decimal confidence = 0.90m,
            decimal? similarity = 0.85m,
            decimal margin = 0.10m)
        {
            return new SystemAcceptanceThreshold(
                "candidate-gate/non-production-v1",
                confidence,
                similarity,
                margin,
                false);
        }

        internal static SemanticCandidateAcceptanceContext Context(
            SystemAcceptanceThreshold threshold = null,
            bool requiresSubject = true,
            string expectedSubjectKey = SubjectKey,
            bool hasNegation = false,
            bool hasHardRuleConflict = false,
            bool evidenceSufficient = true,
            bool similarityRequired = true)
        {
            return new SemanticCandidateAcceptanceContext(
                threshold ?? Threshold(),
                requiresSubject,
                expectedSubjectKey,
                hasNegation,
                hasHardRuleConflict,
                evidenceSufficient,
                similarityRequired);
        }
    }
}
