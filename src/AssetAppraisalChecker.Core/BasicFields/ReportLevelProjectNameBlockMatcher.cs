using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    /// <summary>
    /// Recognizes only the four report-title block shapes certified by PN-AUDIT.
    /// The search is anchored and bounded; it never scans an arbitrary window for
    /// text that merely resembles another project name.
    /// </summary>
    public sealed class ReportLevelProjectNameBlockMatcher
    {
        private const string CoverAnchor = "资产评估报告";
        private const string SummaryAnchor = "资产评估报告摘要";
        private const string BodyAnchor = "资产评估报告正文";
        private const int MaximumCandidateParagraphLength = 100;
        private const int MaximumAssembledLength = 240;

        private static readonly Regex PureDatePattern = new Regex(
            @"^(?:\d{4}年(?:\d{1,2}月(?:\d{1,2}日)?)?|\d{4}[-/.]\d{1,2}[-/.]\d{1,2})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex MoneyPattern = new Regex(
            @"(?:人民币|RMB|[￥¥]|\d[\d,.]*\s*(?:亿元|万元|元)|[零〇一二两三四五六七八九十百千万亿壹贰叁肆伍陆柒捌玖拾佰仟萬億]+(?:亿元|万元|元|圆))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string[] NarrativeOpenings =
        {
            "本段", "正文", "本文", "本报告", "本次评估", "本评估项目",
            "该评估项目", "上述评估项目", "以上", "根据", "经评估", "说明"
        };

        private static readonly string[] PlaceholderValues =
        {
            "无", "暂无", "不适用", "待定", "未填写", "详见", "-", "—", "/"
        };

        public IReadOnlyList<ReportLevelProjectNameCandidate> FindMatches(
            IEnumerable<DocumentElement> elements)
        {
            if (elements == null)
            {
                throw new ArgumentNullException(nameof(elements));
            }

            List<DocumentElement> ordered = elements
                .OrderBy(item => item.DocumentOrderIndex)
                .ToList();
            var matches = new List<ReportLevelProjectNameCandidate>();

            for (int anchorIndex = 0; anchorIndex < ordered.Count; anchorIndex++)
            {
                ReportLevelProjectNameAuthorityRole authorityRole;
                if (!TryGetAuthorityRole(ordered[anchorIndex], out authorityRole))
                {
                    continue;
                }

                ReportLevelProjectNameCandidate candidate;
                if (TryMatchAtAnchor(
                    ordered,
                    anchorIndex,
                    authorityRole,
                    out candidate))
                {
                    matches.Add(candidate);
                }
            }

            return new ReadOnlyCollection<ReportLevelProjectNameCandidate>(matches);
        }

        private static bool TryMatchAtAnchor(
            IReadOnlyList<DocumentElement> ordered,
            int anchorIndex,
            ReportLevelProjectNameAuthorityRole authorityRole,
            out ReportLevelProjectNameCandidate candidate)
        {
            candidate = null;
            DocumentElement anchor = ordered[anchorIndex];
            if (anchorIndex == 0)
            {
                return false;
            }

            DocumentElement immediate = ordered[anchorIndex - 1];
            if (!AreConsecutive(immediate, anchor))
            {
                return false;
            }

            if (IsBlankParagraph(immediate))
            {
                return authorityRole == ReportLevelProjectNameAuthorityRole.Cover &&
                       TryMatchThreeParagraphCoverWithBlank(
                           ordered,
                           anchorIndex,
                           authorityRole,
                           out candidate);
            }

            List<DocumentElement> directParagraphs = CollectDirectParagraphs(
                ordered,
                anchorIndex,
                authorityRole);
            ReportLevelProjectNameExtractionPattern pattern;
            if (directParagraphs.Count == 2)
            {
                bool hasLeadingLineBreak = HasLeadingLineBreak(directParagraphs[0].Text);
                if (hasLeadingLineBreak &&
                    authorityRole != ReportLevelProjectNameAuthorityRole.Body)
                {
                    return false;
                }

                pattern = hasLeadingLineBreak
                    ? ReportLevelProjectNameExtractionPattern
                        .Pattern4TwoParagraphBodyWithLeadingLineBreak
                    : ReportLevelProjectNameExtractionPattern
                        .Pattern1TwoParagraphDirect;
            }
            else if (directParagraphs.Count == 3 &&
                     authorityRole == ReportLevelProjectNameAuthorityRole.Summary)
            {
                pattern = ReportLevelProjectNameExtractionPattern
                    .Pattern3ThreeParagraphSummaryDirect;
            }
            else
            {
                return false;
            }

            return TryCreateCandidate(
                directParagraphs,
                anchor,
                authorityRole,
                pattern,
                out candidate);
        }

        private static bool TryMatchThreeParagraphCoverWithBlank(
            IReadOnlyList<DocumentElement> ordered,
            int anchorIndex,
            ReportLevelProjectNameAuthorityRole authorityRole,
            out ReportLevelProjectNameCandidate candidate)
        {
            candidate = null;
            if (anchorIndex < 4)
            {
                return false;
            }

            DocumentElement blank = ordered[anchorIndex - 1];
            var paragraphs = new List<DocumentElement>
            {
                ordered[anchorIndex - 4],
                ordered[anchorIndex - 3],
                ordered[anchorIndex - 2]
            };
            if (!IsBlankParagraph(blank) ||
                !AreConsecutive(paragraphs[0], paragraphs[1]) ||
                !AreConsecutive(paragraphs[1], paragraphs[2]) ||
                !AreConsecutive(paragraphs[2], blank) ||
                paragraphs.Any(item => !IsCandidateParagraph(item, false)))
            {
                return false;
            }

            // Four title-like paragraphs before the blank separator are not one of
            // the certified patterns and must fail closed.
            if (anchorIndex >= 5 &&
                AreConsecutive(ordered[anchorIndex - 5], paragraphs[0]) &&
                IsCandidateParagraph(ordered[anchorIndex - 5], false))
            {
                return false;
            }

            return TryCreateCandidate(
                paragraphs,
                ordered[anchorIndex],
                authorityRole,
                ReportLevelProjectNameExtractionPattern
                    .Pattern2ThreeParagraphCoverWithBlankSeparator,
                out candidate);
        }

        private static List<DocumentElement> CollectDirectParagraphs(
            IReadOnlyList<DocumentElement> ordered,
            int anchorIndex,
            ReportLevelProjectNameAuthorityRole authorityRole)
        {
            var reversed = new List<DocumentElement>();
            DocumentElement next = ordered[anchorIndex];

            // Looking back at most four elements is deliberate: two and three are
            // certified, while a fourth title-like paragraph is an explicit reject.
            for (int index = anchorIndex - 1;
                 index >= 0 && reversed.Count < 4;
                 index--)
            {
                DocumentElement current = ordered[index];
                bool allowLeadingLineBreak =
                    authorityRole == ReportLevelProjectNameAuthorityRole.Body;
                if (!AreConsecutive(current, next) ||
                    !IsCandidateParagraph(current, allowLeadingLineBreak))
                {
                    break;
                }

                reversed.Add(current);
                next = current;
            }

            reversed.Reverse();
            if (reversed.Count > 0)
            {
                for (int index = 1; index < reversed.Count; index++)
                {
                    if (ContainsLineBreak(reversed[index].Text))
                    {
                        return new List<DocumentElement>();
                    }
                }
            }

            return reversed;
        }

        private static bool TryCreateCandidate(
            IReadOnlyList<DocumentElement> paragraphs,
            DocumentElement anchor,
            ReportLevelProjectNameAuthorityRole authorityRole,
            ReportLevelProjectNameExtractionPattern pattern,
            out ReportLevelProjectNameCandidate candidate)
        {
            candidate = null;
            string assembled = string.Concat(
                paragraphs.Select(item => BasicFieldNormalizer.NormalizeWhitespace(item.Text)));
            if (!IsCompleteReportLevelTitle(assembled))
            {
                return false;
            }

            BasicFieldNormalizationResult normalization = BasicFieldNormalizer.Normalize(
                BasicFieldType.ProjectName,
                assembled);
            if (!normalization.IsSuccess)
            {
                return false;
            }

            candidate = new ReportLevelProjectNameCandidate(
                assembled,
                authorityRole,
                paragraphs,
                anchor,
                pattern,
                normalization);
            return true;
        }

        private static bool TryGetAuthorityRole(
            DocumentElement anchor,
            out ReportLevelProjectNameAuthorityRole authorityRole)
        {
            authorityRole = ReportLevelProjectNameAuthorityRole.Cover;
            if (!IsMainBodyStandaloneParagraphOrHeading(anchor))
            {
                return false;
            }

            string normalized = BasicFieldNormalizer.NormalizeWhitespace(anchor.Text);
            if (string.Equals(normalized, SummaryAnchor, StringComparison.Ordinal))
            {
                authorityRole = ReportLevelProjectNameAuthorityRole.Summary;
                return true;
            }

            if (string.Equals(normalized, BodyAnchor, StringComparison.Ordinal))
            {
                authorityRole = ReportLevelProjectNameAuthorityRole.Body;
                return true;
            }

            if (!string.Equals(normalized, CoverAnchor, StringComparison.Ordinal))
            {
                return false;
            }

            if (anchor.ElementType == DocumentElementType.Paragraph &&
                anchor.HeadingPath.Count == 0)
            {
                authorityRole = ReportLevelProjectNameAuthorityRole.Cover;
                return true;
            }

            if (anchor.ElementType == DocumentElementType.Heading &&
                anchor.HeadingPath.Count > 0 &&
                string.Equals(
                    BasicFieldNormalizer.NormalizeWhitespace(anchor.HeadingPath.Last()),
                    CoverAnchor,
                    StringComparison.Ordinal))
            {
                authorityRole = ReportLevelProjectNameAuthorityRole.Body;
                return true;
            }

            return false;
        }

        private static bool IsCandidateParagraph(
            DocumentElement element,
            bool allowLeadingLineBreak)
        {
            if (!IsMainBodyParagraph(element) || string.IsNullOrWhiteSpace(element.Text))
            {
                return false;
            }

            if (ContainsLineBreak(element.Text) &&
                (!allowLeadingLineBreak || !HasLeadingLineBreakOnly(element.Text)))
            {
                return false;
            }

            string normalized = BasicFieldNormalizer.NormalizeWhitespace(element.Text);
            if (normalized.Length < 2 ||
                normalized.Length > MaximumCandidateParagraphLength ||
                PlaceholderValues.Contains(normalized, StringComparer.Ordinal) ||
                HasSentencePunctuation(normalized) ||
                NarrativeOpenings.Any(item => normalized.StartsWith(item, StringComparison.Ordinal)) ||
                normalized.EndsWith("如下", StringComparison.Ordinal) ||
                PureDatePattern.IsMatch(normalized) ||
                MoneyPattern.IsMatch(normalized) ||
                IsStandaloneReportNumber(normalized) ||
                IsAuthorityText(normalized))
            {
                return false;
            }

            return true;
        }

        private static bool IsCompleteReportLevelTitle(string assembled)
        {
            if (string.IsNullOrWhiteSpace(assembled) ||
                assembled.Length < 8 ||
                assembled.Length > MaximumAssembledLength ||
                !assembled.Contains("拟") ||
                HasSentencePunctuation(assembled))
            {
                return false;
            }

            return assembled.EndsWith("公允价值", StringComparison.Ordinal) ||
                   assembled.EndsWith("市场价值", StringComparison.Ordinal) ||
                   assembled.EndsWith("评估项目", StringComparison.Ordinal);
        }

        private static bool IsStandaloneReportNumber(string normalized)
        {
            int startIndex;
            int length;
            return StandaloneReportNumberMatcher.TryMatch(
                       normalized,
                       out startIndex,
                       out length) &&
                   startIndex == 0 &&
                   length == normalized.Length;
        }

        private static bool IsAuthorityText(string normalized)
        {
            return string.Equals(normalized, CoverAnchor, StringComparison.Ordinal) ||
                   string.Equals(normalized, SummaryAnchor, StringComparison.Ordinal) ||
                   string.Equals(normalized, BodyAnchor, StringComparison.Ordinal);
        }

        private static bool IsBlankParagraph(DocumentElement element)
        {
            return IsMainBodyParagraph(element) &&
                   string.IsNullOrWhiteSpace(element.Text);
        }

        private static bool IsMainBodyParagraph(DocumentElement element)
        {
            return element != null &&
                   element.ElementType == DocumentElementType.Paragraph &&
                   element.Location.DocumentPart == DocumentPart.MainBody &&
                   element.Location.ParagraphIndex.HasValue &&
                   !element.Location.TableIndex.HasValue;
        }

        private static bool IsMainBodyStandaloneParagraphOrHeading(DocumentElement element)
        {
            return element != null &&
                   (element.ElementType == DocumentElementType.Paragraph ||
                    element.ElementType == DocumentElementType.Heading) &&
                   element.Location.DocumentPart == DocumentPart.MainBody &&
                   element.Location.ParagraphIndex.HasValue &&
                   !element.Location.TableIndex.HasValue;
        }

        private static bool AreConsecutive(DocumentElement first, DocumentElement second)
        {
            return first != null &&
                   second != null &&
                   second.DocumentOrderIndex == first.DocumentOrderIndex + 1;
        }

        private static bool HasSentencePunctuation(string text)
        {
            return (text ?? string.Empty).IndexOfAny(
                new[] { '。', '；', ';', '！', '!', '？', '?', '：', ':', '，', ',' }) >= 0;
        }

        private static bool ContainsLineBreak(string text)
        {
            return (text ?? string.Empty).IndexOfAny(new[] { '\r', '\n' }) >= 0;
        }

        private static bool HasLeadingLineBreak(string text)
        {
            string source = text ?? string.Empty;
            int firstContent = 0;
            while (firstContent < source.Length && char.IsWhiteSpace(source[firstContent]))
            {
                firstContent++;
            }

            return source.Substring(0, firstContent).IndexOfAny(new[] { '\r', '\n' }) >= 0;
        }

        private static bool HasLeadingLineBreakOnly(string text)
        {
            string source = text ?? string.Empty;
            int firstContent = 0;
            while (firstContent < source.Length && char.IsWhiteSpace(source[firstContent]))
            {
                firstContent++;
            }

            return source.Substring(0, firstContent).IndexOfAny(new[] { '\r', '\n' }) >= 0 &&
                   source.Substring(firstContent).IndexOfAny(new[] { '\r', '\n' }) < 0;
        }
    }
}
