using System;
using System.Runtime.Serialization;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    [DataContract]
    internal sealed class LocalSemanticAcceptanceDataset
    {
        public LocalSemanticAcceptanceDataset()
        {
            Cases = Array.Empty<LocalSemanticAcceptanceCase>();
        }

        [DataMember(IsRequired = true, Order = 1)]
        public string SchemaVersion { get; set; }

        [DataMember(IsRequired = true, Order = 2)]
        public string DatasetVersion { get; set; }

        [DataMember(IsRequired = true, Order = 3)]
        public string DataClassification { get; set; }

        [DataMember(IsRequired = true, Order = 4)]
        public LocalSemanticAcceptanceCase[] Cases { get; set; }
    }

    [DataContract]
    internal sealed class LocalSemanticAcceptanceCase
    {
        public LocalSemanticAcceptanceCase()
        {
            SimulatedCandidates = Array.Empty<LocalSemanticSimulatedCandidate>();
        }

        [DataMember(IsRequired = true, Order = 1)]
        public string CaseId { get; set; }

        [DataMember(IsRequired = true, Order = 2)]
        public string Category { get; set; }

        [DataMember(IsRequired = true, Order = 3)]
        public string InputText { get; set; }

        [DataMember(IsRequired = true, Order = 4)]
        public string CandidateType { get; set; }

        [DataMember(IsRequired = true, Order = 5)]
        public string SubjectKey { get; set; }

        [DataMember(IsRequired = true, Order = 6)]
        public string ExpectedSubjectKey { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = true, Order = 7)]
        public string ExpectedCandidate { get; set; }

        [DataMember(IsRequired = true, Order = 8)]
        public string ExpectedDecisionClass { get; set; }

        [DataMember(IsRequired = true, Order = 9)]
        public bool IsHardNegative { get; set; }

        [DataMember(IsRequired = true, Order = 10)]
        public bool IsAbstentionExpected { get; set; }

        [DataMember(IsRequired = true, Order = 11)]
        public bool HardRuleProtection { get; set; }

        [DataMember(IsRequired = true, Order = 12)]
        public bool HasNegation { get; set; }

        [DataMember(IsRequired = true, Order = 13)]
        public bool HasHardRuleConflict { get; set; }

        [DataMember(IsRequired = true, Order = 14)]
        public LocalSemanticSimulatedCandidate[] SimulatedCandidates { get; set; }

        [DataMember(IsRequired = true, Order = 15)]
        public string Notes { get; set; }
    }

    [DataContract]
    internal sealed class LocalSemanticSimulatedCandidate
    {
        [DataMember(IsRequired = true, Order = 1)]
        public string NormalizedCandidate { get; set; }

        [DataMember(IsRequired = true, Order = 2)]
        public string DisplayValue { get; set; }

        [DataMember(IsRequired = true, Order = 3)]
        public decimal ModelConfidence { get; set; }

        [DataMember(IsRequired = true, Order = 4)]
        public decimal SimilarityScore { get; set; }
    }

    internal sealed class LoadedLocalSemanticAcceptanceDataset
    {
        public LoadedLocalSemanticAcceptanceDataset(
            LocalSemanticAcceptanceDataset dataset,
            string datasetSha256)
        {
            Dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
            DatasetSha256 = datasetSha256
                ?? throw new ArgumentNullException(nameof(datasetSha256));
        }

        public LocalSemanticAcceptanceDataset Dataset { get; }

        public string DatasetSha256 { get; }
    }
}
