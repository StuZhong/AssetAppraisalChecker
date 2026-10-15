using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal sealed class DeterministicFakeLocalModelAdapter : ILocalModelAdapter
    {
        private const decimal SimulatedLatencyMilliseconds = 5m;

        private readonly string intendedRevision;
        private readonly string fakeModelHash;

        internal DeterministicFakeLocalModelAdapter(
            LocalModelAdapterProfile profile,
            string intendedRevision)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(intendedRevision))
            {
                throw new ArgumentException("Revision is required.", nameof(intendedRevision));
            }

            this.intendedRevision = intendedRevision;
            fakeModelHash = SemanticHash.ComputeSha256(
                "deterministic-fake-adapter|" + profile.ModelId + "|" + intendedRevision);
        }

        public string AdapterVersion => Profile.AdapterVersion;

        public LocalModelAdapterProfile Profile { get; }

        public LocalModelAdapterObservation Analyze(LocalSemanticAcceptanceCase testCase)
        {
            if (testCase == null)
            {
                throw new ArgumentNullException(nameof(testCase));
            }

            SemanticCandidateType type = (SemanticCandidateType)Enum.Parse(
                typeof(SemanticCandidateType),
                testCase.CandidateType,
                false);
            SemanticCandidateRole role = ToRole(type);
            string locationKey = "loc/v1:model-acceptance:"
                + testCase.CaseId.ToLowerInvariant();
            var setIdentity = new SemanticCandidateSetIdentity(
                type,
                testCase.SubjectKey,
                locationKey,
                role);
            SemanticSourceLocation sourceLocation = new SemanticSourceLocation(
                "synthetic-local-model-acceptance-dataset",
                locationKey,
                "SyntheticCase=" + testCase.CaseId);
            var evidence = new SemanticCandidateEvidence(
                sourceLocation,
                testCase.InputText,
                "SyntheticCandidatePhrase",
                SemanticHash.ComputeSha256(testCase.InputText));

            List<SemanticCandidate> candidates = testCase.SimulatedCandidates
                .Select(item => new SemanticCandidate(
                    new SemanticCandidateIdentity(
                        setIdentity,
                        item.NormalizedCandidate),
                    item.DisplayValue,
                    item.NormalizedCandidate,
                    item.SimilarityScore,
                    item.ModelConfidence,
                    sourceLocation,
                    new[] { evidence },
                    "deterministic-fake/" + Profile.ModelId,
                    "pending-revision/" + intendedRevision,
                    AdapterVersion,
                    fakeModelHash,
                    "deterministic fake adapter candidate",
                    SemanticCandidateCreatedBy.DeterministicMock))
                .ToList();
            var candidateSet = new SemanticCandidateSet(setIdentity, candidates);

            SemanticCandidateAnalysis analysis = candidates.Count == 0
                ? SemanticCandidateAnalysis.Abstained(
                    "request/" + testCase.CaseId.ToLowerInvariant(),
                    candidateSet,
                    SemanticCandidateFailureCode.UnsupportedText,
                    "fake.controlled-abstention")
                : SemanticCandidateAnalysis.Succeeded(
                    "request/" + testCase.CaseId.ToLowerInvariant(),
                    candidateSet);

            decimal? margin = CalculateMargin(candidates);
            return new LocalModelAdapterObservation(
                analysis,
                SimulatedLatencyMilliseconds,
                margin);
        }

        private static decimal? CalculateMargin(IEnumerable<SemanticCandidate> candidates)
        {
            SemanticCandidate[] ranked = candidates
                .OrderByDescending(item => item.ModelConfidence)
                .ThenBy(item => item.Identity.CanonicalValue, StringComparer.Ordinal)
                .ToArray();
            return ranked.Length < 2
                ? (decimal?)null
                : ranked[0].ModelConfidence - ranked[1].ModelConfidence;
        }

        private static SemanticCandidateRole ToRole(SemanticCandidateType type)
        {
            switch (type)
            {
                case SemanticCandidateType.EvaluationMethod:
                    return SemanticCandidateRole.AdoptedMethod;
                case SemanticCandidateType.AssessmentObjectAssetType:
                    return SemanticCandidateRole.AssetType;
                case SemanticCandidateType.AssessmentObjectEconomicAction:
                    return SemanticCandidateRole.EconomicAction;
                case SemanticCandidateType.AssessmentObjectScope:
                    return SemanticCandidateRole.Scope;
                case SemanticCandidateType.AssessmentValue:
                    return SemanticCandidateRole.AssessmentValue;
                case SemanticCandidateType.BenchmarkDate:
                    return SemanticCandidateRole.BenchmarkDate;
                case SemanticCandidateType.ReportNumber:
                    return SemanticCandidateRole.ReportNumber;
                case SemanticCandidateType.Quantity:
                    return SemanticCandidateRole.Quantity;
                case SemanticCandidateType.Owner:
                    return SemanticCandidateRole.Owner;
                default:
                    throw new InvalidOperationException(
                        "Unsupported CandidateType: " + type.ToString());
            }
        }
    }
}
