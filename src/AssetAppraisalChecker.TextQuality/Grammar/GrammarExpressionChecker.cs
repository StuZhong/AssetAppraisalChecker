using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.TextQuality.Grammar
{
    public sealed class GrammarExpressionChecker
    {
        private const int MinimumFragmentLength = 10;
        private static readonly Regex ListPrefix = new Regex(
            @"^\s*(?:(?:\d+)[.、．]|[（(](?:\d+|[一二三四五六七八九十]+)[）)]|[一二三四五六七八九十]+、)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly ExpressionRuleCatalog _catalog;
        private readonly SentenceSegmenter _segmenter;
        private readonly TextQualityNormalizer _normalizer;

        public GrammarExpressionChecker()
            : this(ExpressionRuleCatalog.Default, new SentenceSegmenter())
        {
        }

        public GrammarExpressionChecker(
            ExpressionRuleCatalog catalog,
            SentenceSegmenter segmenter)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _segmenter = segmenter ?? throw new ArgumentNullException(nameof(segmenter));
            _normalizer = new TextQualityNormalizer();
        }

        public ExpressionRuleCatalog Catalog => _catalog;

        public GrammarCheckResult Check(DocumentReadResult document, string sourceDocument)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException("文档来源不能为空。", nameof(sourceDocument));
            }

            ProtectedTermRegistry protectedTerms = ProtectedTermRegistry.Build(document);
            var candidates = new List<ExpressionCandidate>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            int paragraphsScanned = 0;
            int sentencesScanned = 0;
            int bodySentences = 0;
            int rulesEvaluated = 0;
            int rawCandidates = 0;
            int skippedHeadings = 0;
            int skippedTables = 0;

            IReadOnlyList<DocumentElement> elements = document.Elements
                .OrderBy(item => item.DocumentOrderIndex)
                .ToList();
            foreach (DocumentElement element in elements)
            {
                if (string.IsNullOrWhiteSpace(element.Text)) continue;
                paragraphsScanned++;
                GrammarParagraphRole role = ClassifyRole(element);
                if (role == GrammarParagraphRole.HeadingLike || role == GrammarParagraphRole.Other)
                {
                    if (role == GrammarParagraphRole.HeadingLike) skippedHeadings++;
                    continue;
                }

                if (role == GrammarParagraphRole.TableParagraph && !IsLongTableExplanation(element.Text))
                {
                    skippedTables++;
                    continue;
                }

                IReadOnlyList<SentenceSpan> sentences = _segmenter.Segment(
                    element,
                    role,
                    protectedTerms);
                sentencesScanned += sentences.Count;
                if (role == GrammarParagraphRole.Body) bodySentences += sentences.Count;

                foreach (SentenceSpan sentence in sentences)
                {
                    if (!IsEligibleSentence(sentence)) continue;
                    string ruleInput = TrimTerminalPunctuation(
                        _normalizer.Normalize(sentence.Text));
                    foreach (ExpressionRule rule in _catalog.Rules)
                    {
                        rulesEvaluated++;
                        Match match = rule.Pattern.Match(ruleInput);
                        if (!match.Success) continue;
                        rawCandidates++;

                        int sentenceLeading = sentence.Text.Length - sentence.Text.TrimStart().Length;
                        int matchStart = sentence.Span.Start + sentenceLeading + match.Index;
                        var evidenceSpan = new TextSpan(matchStart, match.Length);
                        if (protectedTerms.IsFullyProtected(element, evidenceSpan)) continue;
                        if (IsListIntroduction(rule, sentence, elements)) continue;

                        var candidate = new ExpressionCandidate(
                            sourceDocument,
                            sentence,
                            evidenceSpan,
                            NormalizeEvidence(match.Value),
                            new ExpressionEvidence(rule, match.Value));
                        if (identities.Add(candidate.StableIdentity)) candidates.Add(candidate);
                    }
                }
            }

            IReadOnlyList<ExpressionCandidate> ordered = candidates
                .OrderBy(item => item.Sentence.Element.DocumentOrderIndex)
                .ThenBy(item => item.EvidenceSpan.Start)
                .ThenBy(item => item.Evidence.RuleId, StringComparer.Ordinal)
                .ToList();
            return new GrammarCheckResult(
                ordered,
                new GrammarCheckDiagnostics(
                    paragraphsScanned,
                    sentencesScanned,
                    bodySentences,
                    rulesEvaluated,
                    rawCandidates,
                    ordered.Count,
                    skippedHeadings,
                    skippedTables,
                    protectedTerms.Terms.Count));
        }

        private static GrammarParagraphRole ClassifyRole(DocumentElement element)
        {
            if (element.ElementType == DocumentElementType.Heading ||
                ContainsHeadingMarker(element.StyleId))
            {
                return GrammarParagraphRole.HeadingLike;
            }

            if (element.ElementType == DocumentElementType.TableCell)
            {
                return GrammarParagraphRole.TableParagraph;
            }

            string text = (element.Text ?? string.Empty).Trim();
            if (text.Length < 6 || ListPrefix.IsMatch(text))
            {
                return GrammarParagraphRole.Other;
            }

            return GrammarParagraphRole.Body;
        }

        private static bool ContainsHeadingMarker(string styleId)
        {
            string value = styleId ?? string.Empty;
            return value.IndexOf("heading", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("标题", StringComparison.Ordinal) >= 0;
        }

        private static bool IsLongTableExplanation(string text)
        {
            string value = (text ?? string.Empty).Trim();
            return value.Length >= 24 && value.IndexOfAny(new[] { '。', '！', '？', '；' }) >= 0;
        }

        private static bool IsEligibleSentence(SentenceSpan sentence)
        {
            if (sentence.ParagraphRole == GrammarParagraphRole.Body)
            {
                return sentence.Text.Trim().Length >= MinimumFragmentLength;
            }

            return sentence.ParagraphRole == GrammarParagraphRole.TableParagraph &&
                sentence.Text.Trim().Length >= 24;
        }

        private static string TrimTerminalPunctuation(string text)
        {
            string value = (text ?? string.Empty).Trim();
            while (value.Length > 0 && "。！？；.!?;".IndexOf(value[value.Length - 1]) >= 0)
            {
                value = value.Substring(0, value.Length - 1).TrimEnd();
            }

            return value;
        }

        private static string NormalizeEvidence(string value)
        {
            return Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ");
        }

        private static bool IsListIntroduction(
            ExpressionRule rule,
            SentenceSpan sentence,
            IReadOnlyList<DocumentElement> elements)
        {
            if (rule.RuleId != "GE-DANGLE-001" &&
                rule.RuleId != "GE-DANGLE-002" &&
                rule.RuleId != "GE-DANGLE-003" &&
                rule.RuleId != "GE-DANGLE-011")
            {
                return false;
            }

            string value = sentence.Text.Trim();
            bool hasColon = value.EndsWith("：", StringComparison.Ordinal) ||
                value.EndsWith(":", StringComparison.Ordinal);
            if (!hasColon) return false;

            DocumentElement next = elements.FirstOrDefault(item =>
                item.DocumentOrderIndex > sentence.Element.DocumentOrderIndex &&
                !string.IsNullOrWhiteSpace(item.Text));
            if (next == null) return false;
            return next.ElementType == DocumentElementType.TableCell ||
                ListPrefix.IsMatch(next.Text);
        }
    }
}
