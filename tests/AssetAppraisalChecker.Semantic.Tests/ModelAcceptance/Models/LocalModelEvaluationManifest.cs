using System;
using System.Runtime.Serialization;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    [DataContract]
    internal sealed class LocalModelEvaluationManifest
    {
        public LocalModelEvaluationManifest()
        {
            ExpectedTokenizerFiles = Array.Empty<string>();
            ExpectedModelFiles = Array.Empty<string>();
        }

        [DataMember(IsRequired = true, Order = 1)]
        public string SchemaVersion { get; set; }

        [DataMember(IsRequired = true, Order = 2)]
        public string ModelId { get; set; }

        [DataMember(IsRequired = true, Order = 3)]
        public string Publisher { get; set; }

        [DataMember(IsRequired = true, Order = 4)]
        public string ModelType { get; set; }

        [DataMember(IsRequired = true, Order = 5)]
        public string Repository { get; set; }

        [DataMember(IsRequired = true, Order = 6)]
        public string IntendedRevision { get; set; }

        [DataMember(IsRequired = true, Order = 7)]
        public string RevisionPinStrategy { get; set; }

        [DataMember(IsRequired = true, Order = 8)]
        public string License { get; set; }

        [DataMember(IsRequired = true, Order = 9)]
        public string LicenseSource { get; set; }

        [DataMember(IsRequired = true, Order = 10)]
        public string CommercialUseStatus { get; set; }

        [DataMember(IsRequired = true, Order = 11)]
        public bool EligibleForEvaluation { get; set; }

        [DataMember(IsRequired = true, Order = 12)]
        public string ExpectedModelFormat { get; set; }

        [DataMember(IsRequired = true, Order = 13)]
        public string[] ExpectedTokenizerFiles { get; set; }

        [DataMember(IsRequired = true, Order = 14)]
        public string[] ExpectedModelFiles { get; set; }

        [DataMember(IsRequired = true, Order = 15)]
        public int MaximumInputLength { get; set; }

        [DataMember(IsRequired = true, Order = 16)]
        public int EmbeddingDimension { get; set; }

        [DataMember(IsRequired = true, Order = 17)]
        public string NormalizationRequirement { get; set; }

        [DataMember(IsRequired = true, Order = 18)]
        public string QueryPrefixRequirement { get; set; }

        [DataMember(IsRequired = true, Order = 19)]
        public string PassagePrefixRequirement { get; set; }

        [DataMember(IsRequired = true, Order = 20)]
        public string ExpectedRuntime { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = true, Order = 21)]
        public string ExpectedModelHash { get; set; }

        [DataMember(IsRequired = true, Order = 22)]
        public string DownloadStatus { get; set; }

        [DataMember(IsRequired = true, Order = 23)]
        public string QuantizationStatus { get; set; }

        [DataMember(IsRequired = true, Order = 24)]
        public string MetadataVerifiedAtUtc { get; set; }
    }

    internal sealed class LoadedLocalModelEvaluationManifest
    {
        public LoadedLocalModelEvaluationManifest(
            LocalModelEvaluationManifest manifest,
            string manifestSha256)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            ManifestSha256 = manifestSha256
                ?? throw new ArgumentNullException(nameof(manifestSha256));
        }

        public LocalModelEvaluationManifest Manifest { get; }

        public string ManifestSha256 { get; }
    }
}
