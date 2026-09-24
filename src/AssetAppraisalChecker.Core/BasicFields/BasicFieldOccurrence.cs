using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public sealed class BasicFieldOccurrence
    {
        public BasicFieldOccurrence(
            BasicFieldType fieldType,
            string rawText,
            string rawValue,
            BasicFieldNormalizationResult normalization,
            DocumentLocation location,
            DocumentNearbyContext nearbyContext,
            ClassificationConfidence confidence,
            IEnumerable<string> evidence,
            int textStartIndex,
            int textLength)
        {
            if (fieldType == BasicFieldType.Unknown)
            {
                throw new ArgumentException("基础字段类型不能为 Unknown。", nameof(fieldType));
            }

            if (normalization == null)
            {
                throw new ArgumentNullException(nameof(normalization));
            }

            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (nearbyContext == null)
            {
                throw new ArgumentNullException(nameof(nearbyContext));
            }

            if (textStartIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(textStartIndex));
            }

            if (textLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(textLength));
            }

            FieldType = fieldType;
            RawText = rawText ?? string.Empty;
            RawValue = rawValue ?? string.Empty;
            ParseStatus = normalization.Status;
            NormalizedValue = normalization.NormalizedValue;
            FailureReason = normalization.FailureReason;
            Location = location;
            NearbyContext = nearbyContext;
            Confidence = confidence;
            Evidence = new ReadOnlyCollection<string>(
                (evidence ?? Enumerable.Empty<string>())
                    .Concat(normalization.Evidence)
                    .ToList());
            TextStartIndex = textStartIndex;
            TextLength = textLength;
        }

        public BasicFieldType FieldType { get; }

        public string RawText { get; }

        public string RawValue { get; }

        public BasicFieldParseStatus ParseStatus { get; }

        public BasicFieldValue NormalizedValue { get; }

        public string FailureReason { get; }

        public DocumentLocation Location { get; }

        public DocumentNearbyContext NearbyContext { get; }

        public ClassificationConfidence Confidence { get; }

        public IReadOnlyList<string> Evidence { get; }

        public int TextStartIndex { get; }

        public int TextLength { get; }

        public bool HasNormalizedValue =>
            ParseStatus == BasicFieldParseStatus.Success && NormalizedValue != null;
    }
}
