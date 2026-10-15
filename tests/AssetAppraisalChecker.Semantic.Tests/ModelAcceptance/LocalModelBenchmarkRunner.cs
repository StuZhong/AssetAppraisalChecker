using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal sealed class LocalModelBenchmarkRunner
    {
        private readonly ILocalModelAdapter adapter;
        private readonly SemanticCandidateValidator validator;
        private readonly SemanticCandidateAcceptanceGate acceptanceGate;
        private readonly SystemAcceptanceThreshold threshold;

        internal LocalModelBenchmarkRunner(ILocalModelAdapter adapter)
        {
            this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            validator = new SemanticCandidateValidator();
            acceptanceGate = new SemanticCandidateAcceptanceGate(validator);
            threshold = new SystemAcceptanceThreshold(
                "local-model-acceptance/experimental-v1",
                0.90m,
                0.85m,
                0.10m,
                false);
        }

        internal ModelAcceptanceReport Run(
            LoadedLocalModelEvaluationManifest loadedManifest,
            LoadedLocalSemanticAcceptanceDataset loadedDataset,
            DateTimeOffset runTimestamp,
            string machineInfoSummary)
        {
            if (loadedManifest == null || loadedDataset == null)
            {
                throw new ArgumentNullException(
                    loadedManifest == null ? nameof(loadedManifest) : nameof(loadedDataset));
            }

            if (!string.Equals(
                loadedManifest.Manifest.ModelId,
                adapter.Profile.ModelId,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Adapter profile does not match the model manifest.");
            }

            LocalSemanticAcceptanceCase[] cases = loadedDataset.Dataset.Cases
                .OrderBy(item => item.CaseId, StringComparer.Ordinal)
                .ToArray();
            var results = new List<LocalModelBenchmarkCaseResult>();
            foreach (LocalSemanticAcceptanceCase item in cases)
            {
                LocalModelAdapterObservation observation = adapter.Analyze(item);
                SemanticCandidateContractValidationResult validation =
                    validator.Validate(observation.Analysis);
                SemanticCandidateAcceptanceDecision decision = acceptanceGate.Evaluate(
                    observation.Analysis,
                    new SemanticCandidateAcceptanceContext(
                        threshold,
                        true,
                        item.ExpectedSubjectKey,
                        item.HasNegation,
                        item.HasHardRuleConflict,
                        true,
                        true));
                string actualCandidate = observation.Analysis.CandidateSet.Candidates
                    .OrderByDescending(candidate => candidate.ModelConfidence)
                    .ThenBy(
                        candidate => candidate.Identity.CanonicalValue,
                        StringComparer.Ordinal)
                    .Select(candidate => candidate.NormalizedCandidate)
                    .FirstOrDefault();
                results.Add(new LocalModelBenchmarkCaseResult(
                    item.CaseId,
                    item.Category,
                    item.ExpectedCandidate,
                    actualCandidate,
                    decision,
                    validation,
                    observation.LatencyMilliseconds,
                    observation.Top1Top2Margin));
            }

            LocalModelBenchmarkMetrics metrics =
                new LocalModelBenchmarkMetricsCalculator().Calculate(cases, results);
            return new ModelAcceptanceReport(
                loadedDataset.Dataset.DatasetVersion,
                loadedDataset.DatasetSha256,
                loadedManifest.Manifest.ModelId,
                loadedManifest.Manifest.IntendedRevision,
                loadedManifest.Manifest.ExpectedModelHash,
                loadedManifest.ManifestSha256,
                adapter.AdapterVersion,
                "deterministic-fake-runtime/no-onnx/1.0",
                "Pending",
                machineInfoSummary,
                runTimestamp,
                metrics,
                results,
                GateForCategory(cases, results, "Safety"),
                SemanticGate(cases, results),
                HardNegativeGate(cases, results),
                AbstentionGate(cases, results),
                ModelAcceptanceGateStatus.NotEvaluated,
                ModelAcceptanceGateStatus.NotEvaluated,
                loadedManifest.Manifest.EligibleForEvaluation
                    ? ModelAcceptanceGateStatus.Passed
                    : ModelAcceptanceGateStatus.Failed);
        }

        private static ModelAcceptanceGateStatus SemanticGate(
            IEnumerable<LocalSemanticAcceptanceCase> cases,
            IEnumerable<LocalModelBenchmarkCaseResult> results)
        {
            Dictionary<string, LocalModelBenchmarkCaseResult> byId = results
                .ToDictionary(item => item.CaseId, StringComparer.Ordinal);
            bool passed = cases.All(item =>
                byId[item.CaseId].Validation.IsValid
                && string.Equals(
                    byId[item.CaseId].Decision.Decision.ToString(),
                    item.ExpectedDecisionClass,
                    StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(item.ExpectedCandidate)
                    || string.Equals(
                        byId[item.CaseId].ActualCandidate,
                        item.ExpectedCandidate,
                        StringComparison.Ordinal)));
            return passed
                ? ModelAcceptanceGateStatus.Passed
                : ModelAcceptanceGateStatus.Failed;
        }

        private static ModelAcceptanceGateStatus HardNegativeGate(
            IEnumerable<LocalSemanticAcceptanceCase> cases,
            IEnumerable<LocalModelBenchmarkCaseResult> results)
        {
            Dictionary<string, LocalModelBenchmarkCaseResult> byId = results
                .ToDictionary(item => item.CaseId, StringComparer.Ordinal);
            return cases.Where(item => item.IsHardNegative).Any(item =>
                    byId[item.CaseId].Decision.Decision
                    == SemanticCandidateAcceptanceDecisionKind.AcceptedCandidate)
                ? ModelAcceptanceGateStatus.Failed
                : ModelAcceptanceGateStatus.Passed;
        }

        private static ModelAcceptanceGateStatus AbstentionGate(
            IEnumerable<LocalSemanticAcceptanceCase> cases,
            IEnumerable<LocalModelBenchmarkCaseResult> results)
        {
            Dictionary<string, LocalModelBenchmarkCaseResult> byId = results
                .ToDictionary(item => item.CaseId, StringComparer.Ordinal);
            return cases.Where(item => item.IsAbstentionExpected).All(item =>
                    byId[item.CaseId].Decision.Decision
                    == SemanticCandidateAcceptanceDecisionKind.Warning)
                ? ModelAcceptanceGateStatus.Passed
                : ModelAcceptanceGateStatus.Failed;
        }

        private static ModelAcceptanceGateStatus GateForCategory(
            IEnumerable<LocalSemanticAcceptanceCase> cases,
            IEnumerable<LocalModelBenchmarkCaseResult> results,
            string category)
        {
            HashSet<string> ids = new HashSet<string>(
                cases.Where(item => string.Equals(
                        item.Category,
                        category,
                        StringComparison.Ordinal))
                    .Select(item => item.CaseId),
                StringComparer.Ordinal);
            return results.Where(item => ids.Contains(item.CaseId)).Any(item =>
                    item.Decision.Decision
                    == SemanticCandidateAcceptanceDecisionKind.AcceptedCandidate)
                ? ModelAcceptanceGateStatus.Failed
                : ModelAcceptanceGateStatus.Passed;
        }
    }
}
