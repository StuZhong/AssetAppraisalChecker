using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class SpellChecker
    {
        private static readonly Regex ChineseSequence = new Regex(
            @"[一-龥]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NearMatchPrefix = new Regex(
            @"(?:^|[^\p{L}\p{N}])(?:本次|本项|本项目|该项|该项目)(?:核对)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NearMatchSuffix = new Regex(
            @"^(?:为|是|金额)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly ProfessionalLexicon _lexicon;
        private readonly IReadOnlyList<KnownTypoRule> _knownRules;
        private readonly ProfessionalConfusableSet _confusableSet;
        private readonly TextQualityNormalizer _normalizer;

        public SpellChecker()
            : this(
                ProfessionalLexicon.Default,
                new[]
                {
                    new KnownTypoRule(
                        "资产评估报吿",
                        "资产评估报告",
                        "KT-REPORT-001",
                        "受控合成回归中确认的报告术语错字。",
                        KnownTypoSourceCategory.SyntheticRegression),
                    new KnownTypoRule(
                        "评估结伦",
                        "评估结论",
                        "KT-CONCLUSION-001",
                        "受控合成回归中确认的结论术语错字。",
                        KnownTypoSourceCategory.SyntheticRegression),
                    new KnownTypoRule(
                        "评估范国",
                        "评估范围",
                        "KT-SCOPE-001",
                        "受控合成回归中确认的范围术语错字。",
                        KnownTypoSourceCategory.SyntheticRegression)
                },
                ProfessionalConfusableSet.Default)
        {
        }

        public SpellChecker(
            ProfessionalLexicon lexicon,
            IEnumerable<KnownTypoRule> knownRules)
            : this(lexicon, knownRules, ProfessionalConfusableSet.Default)
        {
        }

        public SpellChecker(
            ProfessionalLexicon lexicon,
            IEnumerable<KnownTypoRule> knownRules,
            ProfessionalConfusableSet confusableSet)
        {
            _lexicon = lexicon ?? throw new ArgumentNullException(nameof(lexicon));
            _knownRules = new ReadOnlyCollection<KnownTypoRule>(
                (knownRules ?? throw new ArgumentNullException(nameof(knownRules))).ToList());
            _confusableSet = confusableSet ?? throw new ArgumentNullException(nameof(confusableSet));
            _normalizer = new TextQualityNormalizer();
        }

        public ProfessionalLexicon Lexicon => _lexicon;

        public IReadOnlyList<KnownTypoRule> KnownRules => _knownRules;

        public ProfessionalConfusableSet ConfusableSet => _confusableSet;

        public SpellCheckResult Check(DocumentReadResult document, string sourceDocument)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!document.IsSuccess)
            {
                throw new ArgumentException("文字质量检查只能处理已成功读取的文档。", nameof(document));
            }

            string source = string.IsNullOrWhiteSpace(sourceDocument)
                ? "文档 A"
                : sourceDocument.Trim();
            ProtectedTermRegistry protectedTerms = ProtectedTermRegistry.Build(document);
            var normalizedByElement = new Dictionary<int, string>();
            var exactHitsByElement = new Dictionary<int, IReadOnlyList<ExactTermHit>>();
            var exactTermFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
            int totalChineseTextSpans = 0;
            int exactLexiconHits = 0;

            // One document scan supplies both coverage diagnostics and the frequency evidence.
            foreach (DocumentElement element in document.Elements)
            {
                string normalized = _normalizer.Normalize(element.Text ?? string.Empty);
                normalizedByElement[element.DocumentOrderIndex] = normalized;
                totalChineseTextSpans += ChineseSequence.Matches(normalized).Count;
                IReadOnlyList<ExactTermHit> hits = FindExactProfessionalTerms(normalized);
                exactHitsByElement[element.DocumentOrderIndex] = hits;
                exactLexiconHits += hits.Count;
                foreach (ExactTermHit hit in hits.Where(item =>
                    !protectedTerms.IsProtected(element, item.Span)))
                {
                    exactTermFrequency[hit.Term] = exactTermFrequency.TryGetValue(
                        hit.Term,
                        out int count)
                        ? count + 1
                        : 1;
                }
            }

            var candidates = new List<TypoCandidate>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            int knownTypoHits = 0;
            int nearMatchCandidates = 0;
            foreach (DocumentElement element in document.Elements)
            {
                string original = element.Text ?? string.Empty;
                if (ShouldSkipElement(original))
                {
                    continue;
                }

                string normalized = normalizedByElement[element.DocumentOrderIndex];
                IReadOnlyList<ExactTermHit> exactHits =
                    exactHitsByElement[element.DocumentOrderIndex];
                AddKnownTypoCandidates(
                    candidates,
                    identities,
                    element,
                    source,
                    original,
                    normalized,
                    protectedTerms,
                    exactTermFrequency,
                    ref knownTypoHits);
                AddConfusableCandidates(
                    candidates,
                    identities,
                    element,
                    source,
                    original,
                    normalized,
                    protectedTerms,
                    exactHits,
                    exactTermFrequency,
                    ref nearMatchCandidates);
                AddNearMatchCandidates(
                    candidates,
                    identities,
                    element,
                    source,
                    original,
                    normalized,
                    protectedTerms,
                    exactHits,
                    exactTermFrequency,
                    ref nearMatchCandidates);
            }

            IReadOnlyList<TypoCandidate> reportedCandidates = ConsolidateCandidates(candidates);
            return new SpellCheckResult(
                reportedCandidates,
                new SpellCheckDiagnostics(
                    totalChineseTextSpans,
                    protectedTerms.Terms.Count,
                    exactLexiconHits,
                    knownTypoHits,
                    nearMatchCandidates,
                    reportedCandidates.Count));
        }

        private void AddKnownTypoCandidates(
            ICollection<TypoCandidate> target,
            ISet<string> identities,
            DocumentElement element,
            string source,
            string original,
            string normalized,
            ProtectedTermRegistry protectedTerms,
            IReadOnlyDictionary<string, int> exactTermFrequency,
            ref int hitCount)
        {
            foreach (KnownTypoRule rule in _knownRules.Where(item =>
                _lexicon.Contains(item.SuggestedTerm)))
            {
                int start = 0;
                while ((start = normalized.IndexOf(
                    rule.IncorrectText,
                    start,
                    StringComparison.Ordinal)) >= 0)
                {
                    var span = new TextSpan(start, rule.IncorrectText.Length);
                    if (!protectedTerms.IsProtected(element, span) &&
                        !HasMechanicalRootCause(rule.IncorrectText) &&
                        AddCandidate(
                            target,
                            identities,
                            element,
                            source,
                            original,
                            normalized,
                            span,
                            rule.SuggestedTerm,
                            TypoCandidateKind.KnownTypo,
                            new[]
                            {
                                "受控错字规则：" + rule.RuleId,
                                "规则原因：" + rule.Reason,
                                "规则来源类别：" + rule.SourceCategory,
                                "可能的规范术语：" + rule.SuggestedTerm
                            },
                            new[] { TypoEvidenceReason.KnownTypo },
                            exactTermFrequency))
                    {
                        hitCount++;
                    }

                    start += rule.IncorrectText.Length;
                }
            }
        }

        private void AddConfusableCandidates(
            ICollection<TypoCandidate> target,
            ISet<string> identities,
            DocumentElement element,
            string source,
            string original,
            string normalized,
            ProtectedTermRegistry protectedTerms,
            IReadOnlyList<ExactTermHit> exactHits,
            IReadOnlyDictionary<string, int> exactTermFrequency,
            ref int hitCount)
        {
            foreach (CandidateWindow window in CandidateWindows(normalized))
            {
                if (_lexicon.Contains(window.Text) || HasMechanicalRootCause(window.Text))
                {
                    continue;
                }

                IReadOnlyList<string> matches = _confusableSet.FindLexiconCandidates(
                    window.Text,
                    _lexicon);
                List<string> allNearMatches = _lexicon
                    .GetNearMatchTerms(window.Text.Length)
                    .Where(term => IsSafeEditShape(window.Text, term))
                    .Where(term => EditDistanceAtMostOne(window.Text, term) == 1)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (matches.Count != 1 ||
                    allNearMatches.Count != 1 ||
                    !string.Equals(matches[0], allNearMatches[0], StringComparison.Ordinal) ||
                    IsExcluded(element, window.Span, protectedTerms, exactHits))
                {
                    continue;
                }

                string suggested = matches[0];
                if (AddCandidate(
                    target,
                    identities,
                    element,
                    source,
                    original,
                    normalized,
                    window.Span,
                    suggested,
                    TypoCandidateKind.ConfusableProfessionalTerm,
                    new[]
                    {
                        "疑似形近字符误写，受控规则：" +
                            _confusableSet.GetRuleId(window.Text, suggested),
                        "替换后形成词典中的完整专业术语：“" + suggested + "”。"
                    },
                    new[] { TypoEvidenceReason.ConfusableProfessionalTerm },
                    exactTermFrequency))
                {
                    hitCount++;
                }
            }
        }

        private void AddNearMatchCandidates(
            ICollection<TypoCandidate> target,
            ISet<string> identities,
            DocumentElement element,
            string source,
            string original,
            string normalized,
            ProtectedTermRegistry protectedTerms,
            IReadOnlyList<ExactTermHit> exactHits,
            IReadOnlyDictionary<string, int> exactTermFrequency,
            ref int hitCount)
        {
            foreach (CandidateWindow window in CandidateWindows(normalized))
            {
                if (_lexicon.Contains(window.Text) || HasMechanicalRootCause(window.Text) ||
                    !HasReliableNearMatchBoundary(normalized, window))
                {
                    continue;
                }

                List<string> matches = _lexicon
                    .GetNearMatchTerms(window.Text.Length)
                    .Where(term => IsSafeEditShape(window.Text, term))
                    .Where(term => EditDistanceAtMostOne(window.Text, term) == 1)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (matches.Count != 1 ||
                    IsExcluded(element, window.Span, protectedTerms, exactHits))
                {
                    continue;
                }

                string spellingEvidence = GetNearMatchSpellingEvidence(window.Text, matches[0]);
                if (spellingEvidence == null) continue;

                if (AddCandidate(
                    target,
                    identities,
                    element,
                    source,
                    original,
                    normalized,
                    window.Span,
                    matches[0],
                    TypoCandidateKind.ProfessionalTermNearMatch,
                    new[]
                    {
                        "疑似专业术语误写：与“" + matches[0] + "”的编辑距离为 1。",
                        "最佳候选唯一；建议人工确认。",
                        "候选边界：完整文字片段或受控语句槽位。",
                        spellingEvidence
                    },
                    new[] { TypoEvidenceReason.ProfessionalNearMatch },
                    exactTermFrequency))
                {
                    hitCount++;
                }
            }
        }

        private static bool HasReliableNearMatchBoundary(string text, CandidateWindow window)
        {
            // Chinese has no universal word delimiter. Do not treat every sliding
            // window as a word, or use a tokenizer's guess as authoritative evidence.
            // The short grammatical slots retain existing controlled positives; other
            // sentence constructions deliberately abstain rather than expand coverage.
            int end = window.Span.Start + window.Span.Length;
            bool left = window.Span.Start == 0 ||
                !char.IsLetterOrDigit(text[window.Span.Start - 1]) ||
                NearMatchPrefix.IsMatch(text.Substring(0, window.Span.Start));
            bool right = end == text.Length || !char.IsLetterOrDigit(text[end]) ||
                NearMatchSuffix.IsMatch(text.Substring(end));
            return left && right;
        }

        private static string GetNearMatchSpellingEvidence(string observed, string expected)
        {
            // A unique dictionary neighbour is not proof of a spelling mistake:
            // 使用者/使用人 and 投运/投资 may represent different legitimate concepts.
            // Keep only substitutions attested by the existing controlled positives
            // (直 -> 值, 青 -> 清, 剧 -> 据), or an extra repeated internal character.
            // These are positive error shapes, not a growing whitelist of legal phrases.
            // Unknown substitutions
            // and missing characters have no independent error evidence and abstain.
            if (observed.Length == expected.Length)
            {
                int changed = -1;
                for (int index = 0; index < observed.Length; index++)
                {
                    if (observed[index] == expected[index]) continue;
                    if (changed >= 0) return null;
                    changed = index;
                }

                bool controlled = changed >= 0 &&
                    ((observed[changed] == '直' && expected[changed] == '值') ||
                     (observed[changed] == '青' && expected[changed] == '清') ||
                     (observed[changed] == '剧' && expected[changed] == '据'));
                return controlled
                    ? "错写依据：已有受控正例支持的" + observed[changed] + "→" +
                        expected[changed] + "字符替换；仍需人工复核。"
                    : null;
            }

            if (observed.Length == expected.Length + 1)
            {
                for (int index = 1; index < observed.Length - 1; index++)
                {
                    if ((observed[index] == observed[index - 1] ||
                         observed[index] == observed[index + 1]) &&
                        string.Equals(observed.Remove(index, 1), expected, StringComparison.Ordinal))
                        return "错写依据：完整候选内重复插入同一字符，删除一字后形成唯一规范术语。";
                }
            }

            return null;
        }

        private IEnumerable<CandidateWindow> CandidateWindows(string normalized)
        {
            foreach (Match sequence in ChineseSequence.Matches(normalized))
            {
                for (int offset = 0; offset < sequence.Value.Length; offset++)
                {
                    int maximumLength = Math.Min(16, sequence.Value.Length - offset);
                    for (int length = 4; length <= maximumLength; length++)
                    {
                        yield return new CandidateWindow(
                            sequence.Value.Substring(offset, length),
                            new TextSpan(sequence.Index + offset, length));
                    }
                }
            }
        }

        private IReadOnlyList<ExactTermHit> FindExactProfessionalTerms(string text)
        {
            var hits = new List<ExactTermHit>();
            foreach (string term in _lexicon.Terms)
            {
                int start = 0;
                while ((start = text.IndexOf(term, start, StringComparison.Ordinal)) >= 0)
                {
                    hits.Add(new ExactTermHit(term, new TextSpan(start, term.Length)));
                    start += term.Length;
                }
            }

            return new ReadOnlyCollection<ExactTermHit>(hits);
        }

        private static bool IsExcluded(
            DocumentElement element,
            TextSpan span,
            ProtectedTermRegistry protectedTerms,
            IEnumerable<ExactTermHit> exactHits)
        {
            return protectedTerms.IsProtected(element, span) ||
                exactHits.Any(item => item.Span.Overlaps(span));
        }

        private static bool ShouldSkipElement(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string trimmed = text.Trim();
            return trimmed.All(character =>
                    char.IsDigit(character) ||
                    char.IsWhiteSpace(character) ||
                    char.IsPunctuation(character)) ||
                (trimmed.IndexOf('=') >= 0 && trimmed.Any(char.IsDigit));
        }

        private static bool HasMechanicalRootCause(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Any(char.IsWhiteSpace))
            {
                return true;
            }

            if (text.Length % 2 == 0)
            {
                int half = text.Length / 2;
                if (string.Equals(
                    text.Substring(0, half),
                    text.Substring(half),
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int EditDistanceAtMostOne(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                return 0;
            }

            if (Math.Abs(left.Length - right.Length) > 1)
            {
                return 2;
            }

            if (left.Length == right.Length)
            {
                int differences = 0;
                for (int index = 0; index < left.Length; index++)
                {
                    if (left[index] != right[index] && ++differences > 1)
                    {
                        return 2;
                    }
                }

                return differences;
            }

            string shorter = left.Length < right.Length ? left : right;
            string longer = left.Length < right.Length ? right : left;
            int shortIndex = 0;
            int longIndex = 0;
            int edits = 0;
            while (shortIndex < shorter.Length && longIndex < longer.Length)
            {
                if (shorter[shortIndex] == longer[longIndex])
                {
                    shortIndex++;
                    longIndex++;
                }
                else
                {
                    edits++;
                    longIndex++;
                    if (edits > 1)
                    {
                        return 2;
                    }
                }
            }

            return 1;
        }

        private static bool IsSafeEditShape(string observed, string expected)
        {
            if (observed.Length == expected.Length)
            {
                return true;
            }

            // Insertions/deletions are accepted only inside a stable phrase. This prevents
            // a prefix before punctuation (for example “评估基准、日”) from being treated as
            // a complete term missing its final character.
            return observed.Length > 1 &&
                expected.Length > 1 &&
                observed[0] == expected[0] &&
                observed[observed.Length - 1] == expected[expected.Length - 1];
        }

        private bool AddCandidate(
            ICollection<TypoCandidate> target,
            ISet<string> identities,
            DocumentElement element,
            string source,
            string original,
            string normalized,
            TextSpan span,
            string suggestedTerm,
            TypoCandidateKind kind,
            IEnumerable<string> evidence,
            IEnumerable<TypoEvidenceReason> reasons,
            IReadOnlyDictionary<string, int> exactTermFrequency)
        {
            string identity = string.Concat(
                element.DocumentOrderIndex,
                "|",
                span.Start,
                "|",
                span.Length,
                "|",
                suggestedTerm);
            if (!identities.Add(identity))
            {
                return false;
            }

            var finalEvidence = new List<string>(evidence ?? Enumerable.Empty<string>())
            {
                "专业术语类别：" + _lexicon.GetCategory(suggestedTerm)
            };
            var finalReasons = new List<TypoEvidenceReason>(
                reasons ?? Enumerable.Empty<TypoEvidenceReason>());
            if (exactTermFrequency.TryGetValue(suggestedTerm, out int frequency) && frequency > 0)
            {
                finalEvidence.Add(
                    "文档内部术语一致性支持：同一文档中规范术语“" +
                    suggestedTerm + "”另出现 " + frequency + " 次。");
                finalReasons.Add(TypoEvidenceReason.DocumentConsistencySupported);
            }

            target.Add(new TypoCandidate(
                kind,
                source,
                original.Substring(span.Start, span.Length),
                normalized.Substring(span.Start, span.Length),
                suggestedTerm,
                span,
                element.Location,
                finalEvidence,
                finalReasons));
            return true;
        }

        private static IReadOnlyList<TypoCandidate> ConsolidateCandidates(
            IReadOnlyList<TypoCandidate> candidates)
        {
            List<TypoCandidate> retained = candidates
                .Where(candidate => !candidates.Any(other =>
                    !ReferenceEquals(candidate, other) &&
                    string.Equals(
                        candidate.SuggestedTerm,
                        other.SuggestedTerm,
                        StringComparison.Ordinal) &&
                    SameLocation(candidate.Location, other.Location) &&
                    candidate.Span.Overlaps(other.Span) &&
                    (other.Span.Length > candidate.Span.Length ||
                        (other.Span.Length == candidate.Span.Length &&
                            Priority(other.Kind) > Priority(candidate.Kind)))))
                .OrderBy(item => item.Location.DocumentPart)
                .ThenBy(item => item.Location.ParagraphIndex ?? int.MaxValue)
                .ThenBy(item => item.Location.TableIndex ?? int.MaxValue)
                .ThenBy(item => item.Location.RowIndex ?? int.MaxValue)
                .ThenBy(item => item.Location.CellIndex ?? int.MaxValue)
                .ThenBy(item => item.Span.Start)
                .ToList();
            return new ReadOnlyCollection<TypoCandidate>(retained);
        }

        private static int Priority(TypoCandidateKind kind)
        {
            switch (kind)
            {
                case TypoCandidateKind.KnownTypo:
                    return 3;
                case TypoCandidateKind.ConfusableProfessionalTerm:
                    return 2;
                default:
                    return 1;
            }
        }

        private static bool SameLocation(DocumentLocation left, DocumentLocation right)
        {
            return left.DocumentPart == right.DocumentPart &&
                left.ParagraphIndex == right.ParagraphIndex &&
                left.TableIndex == right.TableIndex &&
                left.RowIndex == right.RowIndex &&
                left.CellIndex == right.CellIndex;
        }

        private sealed class ExactTermHit
        {
            public ExactTermHit(string term, TextSpan span)
            {
                Term = term;
                Span = span;
            }

            public string Term { get; }
            public TextSpan Span { get; }
        }

        private sealed class CandidateWindow
        {
            public CandidateWindow(string text, TextSpan span)
            {
                Text = text;
                Span = span;
            }

            public string Text { get; }
            public TextSpan Span { get; }
        }
    }
}
