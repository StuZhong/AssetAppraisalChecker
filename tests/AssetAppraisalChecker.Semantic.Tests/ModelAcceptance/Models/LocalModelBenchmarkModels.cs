using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal enum ModelAcceptanceGateStatus
    {
        NotEvaluated = 0,
        Passed = 1,
        Failed = 2
    }

    internal enum ModelFinalEligibility
    {
        Pending = 0,
        EligibleForNextStage = 1,
        Rejected = 2
    }

    internal sealed class LocalModelAdapterObservation
    {
        public LocalModelAdapterObservation(
            SemanticCandidateAnalysis analysis,
            decimal latencyMilliseconds,
            decimal? top1Top2Margin)
        {
            Analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
            if (latencyMilliseconds < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(latencyMilliseconds));
            }

            if (top1Top2Margin.HasValue
                && (top1Top2Margin.Value < 0m || top1Top2Margin.Value > 1m))
            {
                throw new ArgumentOutOfRangeException(nameof(top1Top2Margin));
            }

            LatencyMilliseconds = latencyMilliseconds;
            Top1Top2Margin = top1Top2Margin;
        }

        public SemanticCandidateAnalysis Analysis { get; }

        public decimal LatencyMilliseconds { get; }

        public decimal? Top1Top2Margin { get; }
    }

    internal sealed class LocalModelBenchmarkCaseResult
    {
        public LocalModelBenchmarkCaseResult(
            string caseId,
            string category,
            string expectedCandidate,
            string actualCandidate,
            SemanticCandidateAcceptanceDecision decision,
            SemanticCandidateContractValidationResult validation,
            decimal latencyMilliseconds,
            decimal? top1Top2Margin)
        {
            CaseId = caseId ?? throw new ArgumentNullException(nameof(caseId));
            Category = category ?? throw new ArgumentNullException(nameof(category));
            ExpectedCandidate = expectedCandidate;
            ActualCandidate = actualCandidate;
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
            LatencyMilliseconds = latencyMilliseconds;
            Top1Top2Margin = top1Top2Margin;
        }

        public string CaseId { get; }

        public string Category { get; }

        public string ExpectedCandidate { get; }

        public string ActualCandidate { get; }

        public SemanticCandidateAcceptanceDecision Decision { get; }

        public SemanticCandidateContractValidationResult Validation { get; }

        public decimal LatencyMilliseconds { get; }

        public decimal? Top1Top2Margin { get; }
    }

    internal sealed class LocalModelBenchmarkMetrics
    {
        public int TotalCases { get; set; }

        public decimal CandidateAccuracy { get; set; }

        public decimal Precision { get; set; }

        public decimal Recall { get; set; }

        public decimal FalsePositiveRate { get; set; }

        public decimal FalseNegativeRate { get; set; }

        public decimal AbstentionRate { get; set; }

        public decimal HardNegativeAcceptanceRate { get; set; }

        public decimal? AverageTop1Top2Margin { get; set; }

        public decimal AverageLatencyMilliseconds { get; set; }

        public decimal P50LatencyMilliseconds { get; set; }

        public decimal P95LatencyMilliseconds { get; set; }

        public long? PeakMemoryBytes { get; set; }

        public decimal? ModelLoadTimeMilliseconds { get; set; }
    }

    internal sealed class ModelAcceptanceReport
    {
        public ModelAcceptanceReport(
            string datasetVersion,
            string datasetHash,
            string modelId,
            string revision,
            string modelFileHash,
            string modelManifestHash,
            string adapterVersion,
            string runtimeVersion,
            string quantization,
            string machineInfoSummary,
            DateTimeOffset runTimestamp,
            LocalModelBenchmarkMetrics metrics,
            IEnumerable<LocalModelBenchmarkCaseResult> caseResults,
            ModelAcceptanceGateStatus safetyGate,
            ModelAcceptanceGateStatus semanticGate,
            ModelAcceptanceGateStatus hardNegativeGate,
            ModelAcceptanceGateStatus abstentionGate,
            ModelAcceptanceGateStatus performanceGate,
            ModelAcceptanceGateStatus offlineGate,
            ModelAcceptanceGateStatus licenseGate)
        {
            DatasetVersion = Require(datasetVersion, nameof(datasetVersion));
            DatasetHash = Require(datasetHash, nameof(datasetHash));
            ModelId = Require(modelId, nameof(modelId));
            Revision = Require(revision, nameof(revision));
            ModelFileHash = modelFileHash;
            ModelManifestHash = Require(modelManifestHash, nameof(modelManifestHash));
            AdapterVersion = Require(adapterVersion, nameof(adapterVersion));
            RuntimeVersion = Require(runtimeVersion, nameof(runtimeVersion));
            Quantization = Require(quantization, nameof(quantization));
            MachineInfoSummary = Require(machineInfoSummary, nameof(machineInfoSummary));
            RunTimestamp = runTimestamp;
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            if (caseResults == null)
            {
                throw new ArgumentNullException(nameof(caseResults));
            }

            CaseResults = new ReadOnlyCollection<LocalModelBenchmarkCaseResult>(
                caseResults.OrderBy(item => item.CaseId, StringComparer.Ordinal).ToList());
            SafetyGate = safetyGate;
            SemanticGate = semanticGate;
            HardNegativeGate = hardNegativeGate;
            AbstentionGate = abstentionGate;
            PerformanceGate = performanceGate;
            OfflineGate = offlineGate;
            LicenseGate = licenseGate;
            FinalEligibility = AggregateEligibility(new[]
            {
                safetyGate,
                semanticGate,
                hardNegativeGate,
                abstentionGate,
                performanceGate,
                offlineGate,
                licenseGate
            });
        }

        public string DatasetVersion { get; }
        public string DatasetHash { get; }
        public string ModelId { get; }
        public string Revision { get; }
        public string ModelFileHash { get; }
        public string ModelManifestHash { get; }
        public string AdapterVersion { get; }
        public string RuntimeVersion { get; }
        public string Quantization { get; }
        public string MachineInfoSummary { get; }
        public DateTimeOffset RunTimestamp { get; }
        public LocalModelBenchmarkMetrics Metrics { get; }
        public IReadOnlyList<LocalModelBenchmarkCaseResult> CaseResults { get; }
        public ModelAcceptanceGateStatus SafetyGate { get; }
        public ModelAcceptanceGateStatus SemanticGate { get; }
        public ModelAcceptanceGateStatus HardNegativeGate { get; }
        public ModelAcceptanceGateStatus AbstentionGate { get; }
        public ModelAcceptanceGateStatus PerformanceGate { get; }
        public ModelAcceptanceGateStatus OfflineGate { get; }
        public ModelAcceptanceGateStatus LicenseGate { get; }
        public ModelFinalEligibility FinalEligibility { get; }

        internal static ModelFinalEligibility AggregateEligibility(
            IEnumerable<ModelAcceptanceGateStatus> gates)
        {
            ModelAcceptanceGateStatus[] items = gates.ToArray();
            if (items.Any(item => item == ModelAcceptanceGateStatus.Failed))
            {
                return ModelFinalEligibility.Rejected;
            }

            return items.All(item => item == ModelAcceptanceGateStatus.Passed)
                ? ModelFinalEligibility.EligibleForNextStage
                : ModelFinalEligibility.Pending;
        }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", parameterName);
            }

            return value;
        }
    }
}
