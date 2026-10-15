using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class TypoCandidate
    {
        public TypoCandidate(
            TypoCandidateKind kind,
            string sourceDocument,
            string originalText,
            string normalizedText,
            string suggestedTerm,
            TextSpan span,
            DocumentLocation location,
            IEnumerable<string> evidence,
            IEnumerable<TypoEvidenceReason> reasons)
        {
            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException("文档来源不能为空。", nameof(sourceDocument));
            }

            if (string.IsNullOrWhiteSpace(originalText))
            {
                throw new ArgumentException("候选原文不能为空。", nameof(originalText));
            }

            if (string.IsNullOrWhiteSpace(suggestedTerm))
            {
                throw new ArgumentException("建议复核术语不能为空。", nameof(suggestedTerm));
            }

            Kind = kind;
            SourceDocument = sourceDocument;
            OriginalText = originalText;
            NormalizedText = normalizedText ?? string.Empty;
            SuggestedTerm = suggestedTerm;
            Span = span ?? throw new ArgumentNullException(nameof(span));
            Location = location ?? throw new ArgumentNullException(nameof(location));
            Evidence = new ReadOnlyCollection<string>(
                (evidence ?? Enumerable.Empty<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList());
            Reasons = new ReadOnlyCollection<TypoEvidenceReason>(
                (reasons ?? Enumerable.Empty<TypoEvidenceReason>())
                    .Distinct()
                    .ToList());
        }

        public TypoCandidateKind Kind { get; }
        public string SourceDocument { get; }
        public string OriginalText { get; }
        public string NormalizedText { get; }
        public string SuggestedTerm { get; }
        public TextSpan Span { get; }
        public DocumentLocation Location { get; }
        public IReadOnlyList<string> Evidence { get; }
        public IReadOnlyList<TypoEvidenceReason> Reasons { get; }
    }
}
