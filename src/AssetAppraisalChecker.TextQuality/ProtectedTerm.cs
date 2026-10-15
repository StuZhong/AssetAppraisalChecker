using System;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class ProtectedTerm
    {
        public ProtectedTerm(int documentOrderIndex, TextSpan span, string kind, string originalText)
        {
            if (documentOrderIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(documentOrderIndex));
            }

            DocumentOrderIndex = documentOrderIndex;
            Span = span ?? throw new ArgumentNullException(nameof(span));
            Kind = kind ?? string.Empty;
            OriginalText = originalText ?? string.Empty;
        }

        public int DocumentOrderIndex { get; }
        public TextSpan Span { get; }
        public string Kind { get; }
        public string OriginalText { get; }
    }
}
