using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal sealed class LocalModelEvaluationManifestValidator
    {
        internal const string SupportedSchemaVersion =
            "local-model-evaluation-manifest/1.0";

        private static readonly Regex CommitRevisionPattern = new Regex(
            "\\A[a-f0-9]{40}\\z",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> AllowedLicenses = new HashSet<string>(
            new[] { "MIT", "Apache-2.0" },
            StringComparer.Ordinal);

        internal void Validate(LocalModelEvaluationManifest manifest)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            RequireEqual(
                SupportedSchemaVersion,
                manifest.SchemaVersion,
                "Unsupported model manifest schema.");
            Require(manifest.ModelId, nameof(manifest.ModelId));
            Require(manifest.Publisher, nameof(manifest.Publisher));
            RequireEqual("SentenceEmbedding", manifest.ModelType, "Unsupported model type.");
            RequireRepository(manifest.Repository);

            if (!CommitRevisionPattern.IsMatch(manifest.IntendedRevision ?? string.Empty))
            {
                throw new InvalidDataException(
                    "IntendedRevision must be a lowercase 40-character commit SHA; "
                    + "main/latest/floating branches are forbidden.");
            }

            Require(manifest.RevisionPinStrategy, nameof(manifest.RevisionPinStrategy));
            if (manifest.RevisionPinStrategy.IndexOf(
                "FullCommitSha",
                StringComparison.Ordinal) < 0)
            {
                throw new InvalidDataException(
                    "RevisionPinStrategy must explicitly require FullCommitSha.");
            }

            if (!AllowedLicenses.Contains(manifest.License ?? string.Empty))
            {
                throw new InvalidDataException("Model license is unknown or not approved.");
            }

            RequireHttpsUri(manifest.LicenseSource, nameof(manifest.LicenseSource));
            RequireEqual(
                "Allowed",
                manifest.CommercialUseStatus,
                "Commercial use must be explicitly allowed.");
            if (!manifest.EligibleForEvaluation)
            {
                throw new InvalidDataException(
                    "A license-cleared manifest must be explicitly eligible for evaluation.");
            }

            Require(manifest.ExpectedModelFormat, nameof(manifest.ExpectedModelFormat));
            RequireItems(manifest.ExpectedTokenizerFiles, nameof(manifest.ExpectedTokenizerFiles));
            RequireItems(manifest.ExpectedModelFiles, nameof(manifest.ExpectedModelFiles));
            if (manifest.MaximumInputLength <= 0 || manifest.EmbeddingDimension <= 0)
            {
                throw new InvalidDataException(
                    "MaximumInputLength and EmbeddingDimension must be positive.");
            }

            Require(manifest.NormalizationRequirement, nameof(manifest.NormalizationRequirement));
            if (manifest.QueryPrefixRequirement == null
                || manifest.PassagePrefixRequirement == null)
            {
                throw new InvalidDataException("Adapter prefix requirements must be explicit.");
            }

            Require(manifest.ExpectedRuntime, nameof(manifest.ExpectedRuntime));
            Require(manifest.DownloadStatus, nameof(manifest.DownloadStatus));
            Require(manifest.QuantizationStatus, nameof(manifest.QuantizationStatus));
            DateTimeOffset verifiedAt;
            if (!DateTimeOffset.TryParse(manifest.MetadataVerifiedAtUtc, out verifiedAt)
                || verifiedAt.Offset != TimeSpan.Zero)
            {
                throw new InvalidDataException("MetadataVerifiedAtUtc must be a UTC timestamp.");
            }

            ValidateHashState(manifest);
        }

        private static void ValidateHashState(LocalModelEvaluationManifest manifest)
        {
            if (string.Equals(manifest.DownloadStatus, "Pending", StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(manifest.ExpectedModelHash))
                {
                    throw new InvalidDataException(
                        "Pending model must not contain a fabricated ExpectedModelHash.");
                }

                RequireEqual(
                    "Pending",
                    manifest.QuantizationStatus,
                    "Pending model must have Pending quantization status.");
                return;
            }

            if (!string.Equals(manifest.DownloadStatus, "Downloaded", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Unsupported DownloadStatus.");
            }

            if (!LocalModelAcceptanceHash.IsSha256(manifest.ExpectedModelHash))
            {
                throw new InvalidDataException(
                    "Downloaded model must contain a lowercase SHA-256 model hash.");
            }
        }

        private static void RequireRepository(string value)
        {
            Require(value, "Repository");
            if (value.IndexOf('/') <= 0
                || value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Repository must be an exact publisher/model identifier.");
            }
        }

        private static void RequireHttpsUri(string value, string name)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                throw new InvalidDataException(name + " must be an HTTPS URI.");
            }
        }

        private static void RequireItems(IEnumerable<string> values, string name)
        {
            if (values == null)
            {
                throw new InvalidDataException(name + " is required.");
            }

            string[] items = values.ToArray();
            if (items.Length == 0 || items.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException(name + " must contain non-empty items.");
            }

            if (items.Distinct(StringComparer.Ordinal).Count() != items.Length)
            {
                throw new InvalidDataException(name + " must not contain duplicates.");
            }
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(name + " is required.");
            }
        }

        private static void RequireEqual(string expected, string actual, string message)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidDataException(message);
            }
        }
    }
}
