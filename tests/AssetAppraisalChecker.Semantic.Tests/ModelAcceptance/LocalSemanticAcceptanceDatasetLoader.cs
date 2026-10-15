using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal sealed class LocalSemanticAcceptanceDatasetLoader
    {
        internal const string SupportedSchemaVersion =
            "local-semantic-acceptance-dataset/1.0";

        private static readonly Regex CaseIdPattern = new Regex(
            "\\ASEM-[A-Z]{2,4}-[0-9]{3}\\z",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> Categories = new HashSet<string>(
            new[] { "Positive", "HardNegative", "Abstention", "Safety" },
            StringComparer.Ordinal);

        private static readonly HashSet<string> Decisions = new HashSet<string>(
            new[] { "AcceptedCandidate", "Warning", "Rejected" },
            StringComparer.Ordinal);

        internal LoadedLocalSemanticAcceptanceDataset Load(string path)
        {
            string fullPath = Path.GetFullPath(path ?? string.Empty);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Acceptance dataset was not found.", fullPath);
            }

            LocalSemanticAcceptanceDataset dataset = Deserialize(
                File.ReadAllBytes(fullPath));
            Validate(dataset);
            return new LoadedLocalSemanticAcceptanceDataset(
                dataset,
                LocalModelAcceptanceHash.ComputeFileSha256(fullPath));
        }

        internal LocalSemanticAcceptanceDataset Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("Acceptance dataset JSON is required.");
            }

            LocalSemanticAcceptanceDataset dataset = Deserialize(
                Encoding.UTF8.GetBytes(json));
            Validate(dataset);
            return dataset;
        }

        internal void Validate(LocalSemanticAcceptanceDataset dataset)
        {
            if (dataset == null)
            {
                throw new ArgumentNullException(nameof(dataset));
            }

            RequireEqual(SupportedSchemaVersion, dataset.SchemaVersion, "Unsupported schema.");
            Require(dataset.DatasetVersion, nameof(dataset.DatasetVersion));
            RequireEqual("SyntheticOnly", dataset.DataClassification, "Dataset must be synthetic.");
            if (dataset.Cases == null || dataset.Cases.Length == 0)
            {
                throw new InvalidDataException("Dataset must contain cases.");
            }

            if (dataset.Cases.Any(item => item == null))
            {
                throw new InvalidDataException("Dataset cannot contain null cases.");
            }

            string duplicate = dataset.Cases
                .GroupBy(item => item.CaseId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .FirstOrDefault();
            if (duplicate != null)
            {
                throw new InvalidDataException("Duplicate CaseId: " + duplicate);
            }

            foreach (LocalSemanticAcceptanceCase item in dataset.Cases)
            {
                ValidateCase(item);
            }

            LocalModelAcceptancePrivacyValidationResult privacy =
                new LocalModelAcceptancePrivacyValidator().Inspect(dataset.Cases);
            if (!privacy.IsSafe)
            {
                throw new InvalidDataException(
                    "Acceptance dataset privacy validation failed: "
                    + string.Join(",", privacy.RedactedFindings));
            }
        }

        private static void ValidateCase(LocalSemanticAcceptanceCase item)
        {
            if (!CaseIdPattern.IsMatch(item.CaseId ?? string.Empty))
            {
                throw new InvalidDataException("Invalid stable CaseId.");
            }

            if (!Categories.Contains(item.Category ?? string.Empty))
            {
                throw new InvalidDataException("Invalid Category for " + item.CaseId);
            }

            Require(item.InputText, nameof(item.InputText));
            SemanticCandidateType candidateType;
            if (!Enum.TryParse(item.CandidateType, false, out candidateType)
                || candidateType == SemanticCandidateType.Unknown)
            {
                throw new InvalidDataException("Invalid CandidateType for " + item.CaseId);
            }

            Require(item.SubjectKey, nameof(item.SubjectKey));
            Require(item.ExpectedSubjectKey, nameof(item.ExpectedSubjectKey));
            if (!Decisions.Contains(item.ExpectedDecisionClass ?? string.Empty))
            {
                throw new InvalidDataException("Invalid ExpectedDecisionClass.");
            }

            if (item.SimulatedCandidates == null)
            {
                throw new InvalidDataException("SimulatedCandidates is required.");
            }

            foreach (LocalSemanticSimulatedCandidate candidate in item.SimulatedCandidates)
            {
                if (candidate == null
                    || string.IsNullOrWhiteSpace(candidate.NormalizedCandidate)
                    || string.IsNullOrWhiteSpace(candidate.DisplayValue)
                    || candidate.ModelConfidence < 0m
                    || candidate.ModelConfidence > 1m
                    || candidate.SimilarityScore < 0m
                    || candidate.SimilarityScore > 1m)
                {
                    throw new InvalidDataException(
                        "Invalid simulated candidate for " + item.CaseId);
                }
            }

            if (item.SimulatedCandidates
                .GroupBy(candidate => candidate.NormalizedCandidate, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                throw new InvalidDataException("Duplicate simulated candidate.");
            }

            Require(item.Notes, nameof(item.Notes));
            ValidateCategoryContract(item);
        }

        private static void ValidateCategoryContract(LocalSemanticAcceptanceCase item)
        {
            if (string.Equals(item.Category, "Positive", StringComparison.Ordinal))
            {
                if (item.IsHardNegative
                    || item.IsAbstentionExpected
                    || item.HardRuleProtection
                    || string.IsNullOrWhiteSpace(item.ExpectedCandidate)
                    || !string.Equals(
                        item.ExpectedDecisionClass,
                        "AcceptedCandidate",
                        StringComparison.Ordinal)
                    || !item.SimulatedCandidates.Any(candidate => string.Equals(
                        candidate.NormalizedCandidate,
                        item.ExpectedCandidate,
                        StringComparison.Ordinal)))
                {
                    throw new InvalidDataException("Invalid Positive contract.");
                }
            }
            else if (string.Equals(item.Category, "HardNegative", StringComparison.Ordinal))
            {
                if (!item.IsHardNegative
                    || item.IsAbstentionExpected
                    || !string.Equals(
                        item.ExpectedDecisionClass,
                        "Rejected",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Invalid HardNegative contract.");
                }
            }
            else if (string.Equals(item.Category, "Abstention", StringComparison.Ordinal))
            {
                if (item.IsHardNegative
                    || !item.IsAbstentionExpected
                    || !string.Equals(
                        item.ExpectedDecisionClass,
                        "Warning",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Invalid Abstention contract.");
                }
            }
            else if (!item.HardRuleProtection
                || !string.Equals(
                    item.ExpectedDecisionClass,
                    "Rejected",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Invalid Safety contract.");
            }
        }

        private static LocalSemanticAcceptanceDataset Deserialize(byte[] bytes)
        {
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                {
                    return (LocalSemanticAcceptanceDataset)new DataContractJsonSerializer(
                        typeof(LocalSemanticAcceptanceDataset)).ReadObject(stream);
                }
            }
            catch (SerializationException exception)
            {
                throw new InvalidDataException("Invalid acceptance dataset JSON.", exception);
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
