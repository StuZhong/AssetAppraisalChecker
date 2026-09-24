using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public sealed class BasicFieldNormalizationResult
    {
        private BasicFieldNormalizationResult(
            BasicFieldParseStatus status,
            BasicFieldValue normalizedValue,
            string failureReason,
            IEnumerable<string> evidence)
        {
            Status = status;
            NormalizedValue = normalizedValue;
            FailureReason = failureReason ?? string.Empty;
            Evidence = new ReadOnlyCollection<string>(
                (evidence ?? Enumerable.Empty<string>()).ToList());
        }

        public BasicFieldParseStatus Status { get; }

        public BasicFieldValue NormalizedValue { get; }

        public string FailureReason { get; }

        public IReadOnlyList<string> Evidence { get; }

        public bool IsSuccess => Status == BasicFieldParseStatus.Success && NormalizedValue != null;

        public static BasicFieldNormalizationResult Succeeded(
            BasicFieldValue normalizedValue,
            params string[] evidence)
        {
            return new BasicFieldNormalizationResult(
                BasicFieldParseStatus.Success,
                normalizedValue ?? throw new ArgumentNullException(nameof(normalizedValue)),
                string.Empty,
                evidence);
        }

        public static BasicFieldNormalizationResult Failed(string reason)
        {
            return new BasicFieldNormalizationResult(
                BasicFieldParseStatus.Failed,
                null,
                reason,
                null);
        }
    }
}
