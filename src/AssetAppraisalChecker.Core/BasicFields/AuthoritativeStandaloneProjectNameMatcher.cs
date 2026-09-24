using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    /// <summary>
    /// Matches a complete project name that is presented as a standalone title
    /// immediately before a finite, authoritative asset-appraisal report heading.
    /// The deliberately narrow adjacency rule prevents ordinary narrative mentions
    /// ending in “评估项目” from becoming comparable ProjectName occurrences.
    /// </summary>
    internal static class AuthoritativeStandaloneProjectNameMatcher
    {
        private const string RequiredSuffix = "评估项目";
        private const int MinimumCandidateLength = 8;
        private const int MaximumCandidateLength = 100;

        private static readonly string[] CoverAuthorityAnchors =
        {
            "资产评估报告",
            "资产评估报告正文"
        };

        private const string SummaryAuthorityAnchor = "资产评估报告摘要";

        private static readonly string[] NarrativeOpenings =
        {
            "本段",
            "正文",
            "本文",
            "本报告",
            "本次评估",
            "本评估项目",
            "该评估项目",
            "上述评估项目"
        };

        private static readonly IReadOnlyList<AuthoritativeStandaloneProjectNameMatch> NoMatches =
            new ReadOnlyCollection<AuthoritativeStandaloneProjectNameMatch>(
                new AuthoritativeStandaloneProjectNameMatch[0]);

        public static IReadOnlyList<AuthoritativeStandaloneProjectNameMatch> FindMatches(
            IEnumerable<DocumentElement> elements)
        {
            if (elements == null)
            {
                throw new ArgumentNullException(nameof(elements));
            }

            List<DocumentElement> ordered = elements
                .OrderBy(item => item.DocumentOrderIndex)
                .ToList();
            var matches = new List<AuthoritativeStandaloneProjectNameMatch>();

            for (int index = 0; index + 1 < ordered.Count; index++)
            {
                DocumentElement candidate = ordered[index];
                DocumentElement anchor = ordered[index + 1];
                int startIndex;
                int length;
                string authorityKind;
                if (!IsImmediatelyAdjacent(candidate, anchor) ||
                    !TryGetAuthorityKind(anchor, out authorityKind))
                {
                    continue;
                }

                AuthoritativeProjectNameMatchKind matchKind;
                if (TryGetLegacyCandidateBoundary(candidate, out startIndex, out length))
                {
                    matchKind = AuthoritativeProjectNameMatchKind.HeadingScopedSuffix;
                }
                else if (TryGetAuthoritativeAdjacentCandidateBoundary(
                             candidate,
                             anchor,
                             ordered,
                             out startIndex,
                             out length))
                {
                    matchKind = AuthoritativeProjectNameMatchKind.AuthoritativeAdjacentStandalone;
                }
                else
                {
                    continue;
                }

                matches.Add(new AuthoritativeStandaloneProjectNameMatch(
                    candidate,
                    startIndex,
                    length,
                    authorityKind,
                    matchKind));
            }

            return matches.Count == 0
                ? NoMatches
                : new ReadOnlyCollection<AuthoritativeStandaloneProjectNameMatch>(matches);
        }

        private static bool TryGetLegacyCandidateBoundary(
            DocumentElement element,
            out int startIndex,
            out int length)
        {
            startIndex = 0;
            length = 0;
            if (!IsMainBodyParagraph(element))
            {
                return false;
            }

            // The audited authoritative titles occur inside an established report
            // structure. Requiring existing heading context excludes an otherwise
            // similar unscoped title near the beginning of the same document.
            if (element.HeadingPath.Count == 0)
            {
                return false;
            }

            string text = element.Text ?? string.Empty;
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
            {
                startIndex++;
            }

            int endIndex = text.Length;
            while (endIndex > startIndex && char.IsWhiteSpace(text[endIndex - 1]))
            {
                endIndex--;
            }

            length = endIndex - startIndex;
            if (length == 0)
            {
                return false;
            }

            string candidate = text.Substring(startIndex, length);
            string normalized = BasicFieldNormalizer.NormalizeWhitespace(candidate);
            return normalized.Length >= MinimumCandidateLength &&
                   normalized.Length <= MaximumCandidateLength &&
                   normalized.EndsWith(RequiredSuffix, StringComparison.Ordinal) &&
                   !HasNarrativePunctuationOrLineBreak(candidate);
        }

        private static bool TryGetAuthoritativeAdjacentCandidateBoundary(
            DocumentElement candidateElement,
            DocumentElement anchor,
            IEnumerable<DocumentElement> ordered,
            out int startIndex,
            out int length)
        {
            startIndex = 0;
            length = 0;
            if (!IsMainBodyParagraph(candidateElement) ||
                !HasUniqueImmediateAuthorityAnchor(candidateElement, anchor, ordered))
            {
                return false;
            }

            string text = candidateElement.Text ?? string.Empty;
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
            {
                startIndex++;
            }

            int endIndex = text.Length;
            while (endIndex > startIndex && char.IsWhiteSpace(text[endIndex - 1]))
            {
                endIndex--;
            }

            length = endIndex - startIndex;
            if (length == 0)
            {
                return false;
            }

            string candidate = text.Substring(startIndex, length);
            string normalized = BasicFieldNormalizer.NormalizeWhitespace(candidate);
            string ignoredAuthorityKind;
            return normalized.Length >= MinimumCandidateLength &&
                   normalized.Length <= MaximumCandidateLength &&
                   !HasNarrativePunctuationOrLineBreak(candidate) &&
                   !HasAdjacentPathAmbiguity(candidate) &&
                   !HasNarrativeOpening(normalized) &&
                   !TryGetAuthorityKind(candidateElement, out ignoredAuthorityKind);
        }

        private static bool HasAdjacentPathAmbiguity(string candidate)
        {
            // Without a label or the established suffix, numeric identifiers and
            // enumeration delimiters can denote an abbreviated asset caption rather
            // than the complete comparable project name. Keep only the new path
            // fail-closed; the established heading/suffix path remains unchanged.
            return candidate.Any(char.IsDigit) ||
                   candidate.IndexOf('、') >= 0;
        }

        private static bool HasNarrativeOpening(string normalized)
        {
            return NarrativeOpenings.Any(opening =>
                normalized.StartsWith(opening, StringComparison.Ordinal));
        }

        private static bool HasUniqueImmediateAuthorityAnchor(
            DocumentElement candidate,
            DocumentElement anchor,
            IEnumerable<DocumentElement> ordered)
        {
            List<DocumentElement> immediateElements = ordered
                .Where(item =>
                    item.DocumentOrderIndex == candidate.DocumentOrderIndex + 1)
                .ToList();
            if (immediateElements.Count != 1 ||
                !ReferenceEquals(immediateElements[0], anchor))
            {
                return false;
            }

            // Consecutive authoritative anchors make the intended relationship
            // ambiguous. The new suffix-free path must fail closed in that shape.
            return !ordered.Any(item =>
            {
                if (item.DocumentOrderIndex != anchor.DocumentOrderIndex + 1)
                {
                    return false;
                }

                string ignoredAuthorityKind;
                return TryGetAuthorityKind(item, out ignoredAuthorityKind);
            });
        }

        private static bool IsImmediatelyAdjacent(
            DocumentElement candidate,
            DocumentElement anchor)
        {
            return anchor.DocumentOrderIndex == candidate.DocumentOrderIndex + 1;
        }

        private static bool TryGetAuthorityKind(
            DocumentElement element,
            out string authorityKind)
        {
            authorityKind = string.Empty;
            if (!IsMainBodyTitleElement(element))
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

        private static bool IsMainBodyParagraph(DocumentElement element)
        {
            return element != null &&
                   element.ElementType == DocumentElementType.Paragraph &&
                   element.Location.DocumentPart == DocumentPart.MainBody;
        }

        private static bool IsMainBodyTitleElement(DocumentElement element)
        {
            return element != null &&
                   (element.ElementType == DocumentElementType.Paragraph ||
                    element.ElementType == DocumentElementType.Heading) &&
                   element.Location.DocumentPart == DocumentPart.MainBody;
        }

        private static bool HasNarrativePunctuationOrLineBreak(string text)
        {
            return text.IndexOfAny(new[]
            {
                '\r', '\n', '。', '！', '？', '!', '?', '；', ';', '，', ',', '：', ':'
            }) >= 0;
        }
    }

    internal sealed class AuthoritativeStandaloneProjectNameMatch
    {
        public AuthoritativeStandaloneProjectNameMatch(
            DocumentElement valueElement,
            int startIndex,
            int length,
            string authorityKind,
            AuthoritativeProjectNameMatchKind matchKind)
        {
            ValueElement = valueElement ?? throw new ArgumentNullException(nameof(valueElement));
            StartIndex = startIndex;
            Length = length;
            AuthorityKind = authorityKind ?? string.Empty;
            MatchKind = matchKind;
        }

        public DocumentElement ValueElement { get; }

        public int StartIndex { get; }

        public int Length { get; }

        public string AuthorityKind { get; }

        public AuthoritativeProjectNameMatchKind MatchKind { get; }
    }

    internal enum AuthoritativeProjectNameMatchKind
    {
        HeadingScopedSuffix = 0,
        AuthoritativeAdjacentStandalone = 1
    }
}
