using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    /// <summary>
    /// Matches only the audited standalone report-number form whose sequence is one
    /// ASCII letter followed by exactly three digits. Unlike the established numeric
    /// form, this branch additionally requires an immediately preceding, unique and
    /// exact authoritative report-title anchor to avoid matching other document IDs.
    /// </summary>
    internal static class AuthoritativeAdjacentAlphanumericReportNumberMatcher
    {
        private static readonly string[] CoverAuthorityAnchors =
        {
            "资产评估报告",
            "资产评估报告正文"
        };

        private const string SummaryAuthorityAnchor = "资产评估报告摘要";

        private static readonly IReadOnlyList<AuthoritativeAdjacentAlphanumericReportNumberMatch>
            NoMatches = new ReadOnlyCollection<AuthoritativeAdjacentAlphanumericReportNumberMatch>(
                new AuthoritativeAdjacentAlphanumericReportNumberMatch[0]);

        public static IReadOnlyList<AuthoritativeAdjacentAlphanumericReportNumberMatch> FindMatches(
            IEnumerable<DocumentElement> elements)
        {
            if (elements == null)
            {
                throw new ArgumentNullException(nameof(elements));
            }

            List<DocumentElement> ordered = elements
                .OrderBy(item => item.DocumentOrderIndex)
                .ToList();
            var matches = new List<AuthoritativeAdjacentAlphanumericReportNumberMatch>();

            foreach (DocumentElement candidate in ordered)
            {
                int startIndex;
                int length;
                if (!IsMainBodyParagraph(candidate) ||
                    !StandaloneReportNumberMatcher.TryMatchAuthoritativeAlphanumeric(
                        candidate.Text,
                        out startIndex,
                        out length))
                {
                    continue;
                }

                List<DocumentElement> previousElements = ordered
                    .Where(item =>
                        item.DocumentOrderIndex == candidate.DocumentOrderIndex - 1)
                    .ToList();
                string authorityKind;
                if (previousElements.Count != 1 ||
                    !TryGetAuthorityKind(previousElements[0], out authorityKind))
                {
                    continue;
                }

                matches.Add(new AuthoritativeAdjacentAlphanumericReportNumberMatch(
                    candidate,
                    startIndex,
                    length,
                    authorityKind));
            }

            return matches.Count == 0
                ? NoMatches
                : new ReadOnlyCollection<AuthoritativeAdjacentAlphanumericReportNumberMatch>(
                    matches);
        }

        private static bool IsMainBodyParagraph(DocumentElement element)
        {
            return element != null &&
                   element.ElementType == DocumentElementType.Paragraph &&
                   element.Location.DocumentPart == DocumentPart.MainBody;
        }

        private static bool TryGetAuthorityKind(
            DocumentElement element,
            out string authorityKind)
        {
            authorityKind = string.Empty;
            if (element == null ||
                (element.ElementType != DocumentElementType.Paragraph &&
                 element.ElementType != DocumentElementType.Heading) ||
                element.Location.DocumentPart != DocumentPart.MainBody)
            {
                return false;
            }

            string normalized = BasicFieldNormalizer.NormalizeWhitespace(element.Text);
            if (string.Equals(normalized, SummaryAuthorityAnchor, StringComparison.Ordinal))
            {
                authorityKind = "摘要式权威标题";
                return true;
            }

            if (CoverAuthorityAnchors.Contains(normalized, StringComparer.Ordinal))
            {
                authorityKind = "首页式权威标题";
                return true;
            }

            return false;
        }
    }

    internal sealed class AuthoritativeAdjacentAlphanumericReportNumberMatch
    {
        public AuthoritativeAdjacentAlphanumericReportNumberMatch(
            DocumentElement valueElement,
            int startIndex,
            int length,
            string authorityKind)
        {
            ValueElement = valueElement ?? throw new ArgumentNullException(nameof(valueElement));
            StartIndex = startIndex;
            Length = length;
            AuthorityKind = authorityKind ?? string.Empty;
        }

        public DocumentElement ValueElement { get; }

        public int StartIndex { get; }

        public int Length { get; }

        public string AuthorityKind { get; }
    }
}
