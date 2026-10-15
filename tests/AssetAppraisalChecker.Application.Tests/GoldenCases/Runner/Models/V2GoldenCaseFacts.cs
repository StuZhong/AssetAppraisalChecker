using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models
{
    public sealed class V2GoldenCaseFactGroupAssertion
    {
        public V2GoldenCaseFactGroupAssertion()
        {
            DocumentId = string.Empty;
            FactType = string.Empty;
            Facts = Array.Empty<V2GoldenCaseFactAssertion>();
        }

        public string DocumentId { get; set; }

        public string FactType { get; set; }

        public int ExpectedCount { get; set; }

        public bool AllowAdditionalFacts { get; set; }

        public V2GoldenCaseFactAssertion[] Facts { get; set; }
    }

    public sealed class V2GoldenCaseFactAssertion
    {
        public V2GoldenCaseFactAssertion()
        {
            SourceDocument = string.Empty;
            OriginalText = string.Empty;
            ConfidenceMinimum = string.Empty;
            DocumentPart = string.Empty;
            Fields = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public string SourceDocument { get; set; }

        public string OriginalText { get; set; }

        public string ConfidenceMinimum { get; set; }

        public string DocumentPart { get; set; }

        public int? ParagraphIndex { get; set; }

        public IDictionary<string, string> Fields { get; set; }
    }

    public sealed class V2GoldenCaseFactSnapshot
    {
        public V2GoldenCaseFactSnapshot(
            string documentId,
            FactType factType,
            string sourceDocument,
            string originalText,
            decimal confidence,
            string documentPart,
            int? paragraphIndex,
            IDictionary<string, string> fields)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("DocumentId is required.", nameof(documentId));
            }

            if (factType == FactType.Unknown)
            {
                throw new ArgumentOutOfRangeException(nameof(factType));
            }

            DocumentId = documentId;
            FactType = factType;
            SourceDocument = sourceDocument ?? string.Empty;
            OriginalText = originalText ?? string.Empty;
            Confidence = confidence;
            DocumentPart = documentPart ?? string.Empty;
            ParagraphIndex = paragraphIndex;
            Fields = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(
                    fields ?? new Dictionary<string, string>(),
                    StringComparer.Ordinal));
        }

        public string DocumentId { get; }

        public FactType FactType { get; }

        public string SourceDocument { get; }

        public string OriginalText { get; }

        public decimal Confidence { get; }

        public string DocumentPart { get; }

        public int? ParagraphIndex { get; }

        public IReadOnlyDictionary<string, string> Fields { get; }
    }
}
