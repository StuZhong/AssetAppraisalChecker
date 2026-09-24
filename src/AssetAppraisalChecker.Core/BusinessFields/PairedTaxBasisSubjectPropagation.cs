using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    /// <summary>
    /// Propagates one confirmed asset subject across an explicitly paired
    /// tax-excluded/tax-included appraised-value presentation. The rule is deliberately
    /// paragraph-local and fails closed when the sentence contains competing subjects,
    /// another money meaning, or presentation text outside the audited grammar.
    /// </summary>
    internal static class PairedTaxBasisSubjectPropagation
    {
        private const string HardSentenceBoundaries = "；;。！？!?\r\n";

        private static readonly string[] TaxExcludedLabels =
        {
            "不含增值税评估净值",
            "不含税评估净值",
            "不含增值税评估价值",
            "不含增值税评估值",
            "不含税评估价值",
            "不含税评估值"
        };

        private static readonly string[] TaxIncludedLabels =
        {
            "含增值税评估净值",
            "含税评估净值",
            "含增值税评估价值",
            "含增值税评估值",
            "含税评估价值",
            "含税评估值"
        };

        private static readonly string[] SafePresentationWords =
        {
            "人民币", "大写", "小写", "金额", "亿元", "万元",
            "为", "是", "即", "元", "圆", "整", "正"
        };

        private static readonly string[] AppraisedNetValueSafePresentationWords =
        {
            // “合计” is the explicit Subtotal marker in the audited paired-net-value
            // grammar. Keep it category-specific so the general presentation grammar
            // for the existing AppraisedValue rule is not broadened.
            "合计"
        };

        public static IReadOnlyList<MoneyOccurrence> Apply(
            DocumentElement element,
            IEnumerable<MoneyOccurrence> sourceOccurrences)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (sourceOccurrences == null)
            {
                throw new ArgumentNullException(nameof(sourceOccurrences));
            }

            List<MoneyOccurrence> occurrences = sourceOccurrences
                .OrderBy(item => item.TextStartIndex)
                .ToList();
            if (element.ElementType != DocumentElementType.Paragraph || occurrences.Count < 2)
            {
                return new ReadOnlyCollection<MoneyOccurrence>(occurrences);
            }

            foreach (TextWindow sentence in FindSentences(element.Text))
            {
                ApplyWithinSentence(element.Text, sentence, occurrences);
            }

            return new ReadOnlyCollection<MoneyOccurrence>(occurrences);
        }

        private static void ApplyWithinSentence(
            string paragraphText,
            TextWindow sentence,
            IList<MoneyOccurrence> paragraphOccurrences)
        {
            List<MoneyOccurrence> sentenceOccurrences = paragraphOccurrences
                .Where(item => sentence.Contains(item.TextStartIndex, item.TextLength))
                .OrderBy(item => item.TextStartIndex)
                .ToList();
            if (sentenceOccurrences.Count < 2)
            {
                return;
            }

            TextWindow taxExcludedLabel;
            TextWindow taxIncludedLabel;
            if (!TryFindUniqueLabel(
                    paragraphText,
                    sentence,
                    TaxExcludedLabels,
                    false,
                    out taxExcludedLabel) ||
                !TryFindUniqueLabel(
                    paragraphText,
                    sentence,
                    TaxIncludedLabels,
                    true,
                    out taxIncludedLabel) ||
                taxExcludedLabel.End > taxIncludedLabel.Start)
            {
                return;
            }

            List<MoneyOccurrence> sources = sentenceOccurrences
                .Where(item => item.TextStartIndex >= taxExcludedLabel.End &&
                               item.TextStartIndex + item.TextLength <= taxIncludedLabel.Start)
                .ToList();
            List<MoneyOccurrence> targets = sentenceOccurrences
                .Where(item => item.TextStartIndex >= taxIncludedLabel.End)
                .ToList();

            if (sources.Count == 0 ||
                targets.Count == 0 ||
                sources.Count + targets.Count != sentenceOccurrences.Count ||
                !SourcesAreUnambiguous(sources) ||
                !TargetsAreEligibleForPropagation(
                    targets,
                    sources[0].FieldIdentity.ValueCategory,
                    sources[0].FieldIdentity.Scope) ||
                HasBlockingBusinessField(
                    paragraphText,
                    sentence,
                    taxExcludedLabel,
                    taxIncludedLabel) ||
                !HasOnlySafePresentationText(
                    paragraphText,
                    sentence,
                    taxExcludedLabel,
                    taxIncludedLabel,
                    sentenceOccurrences,
                    sources[0].FieldIdentity.ValueCategory))
            {
                return;
            }

            MoneyOccurrence source = sources[0];
            foreach (MoneyOccurrence target in targets)
            {
                int targetIndex = paragraphOccurrences.IndexOf(target);
                paragraphOccurrences[targetIndex] = WithPropagatedSubject(target, source);
            }
        }

        private static bool SourcesAreUnambiguous(IReadOnlyCollection<MoneyOccurrence> sources)
        {
            return sources.All(item =>
                       IsSupportedValueCategory(item.FieldIdentity.ValueCategory) &&
                       item.FieldIdentity.TaxBasis == TaxBasis.TaxExcluded &&
                       item.ValueCategoryConfidence == ClassificationConfidence.High &&
                       item.TaxBasisConfidence == ClassificationConfidence.High &&
                       item.SubjectKind == SubjectKind.Asset &&
                       item.SubjectConfidence == ClassificationConfidence.High &&
                       !string.IsNullOrWhiteSpace(item.SubjectId)) &&
                   sources.Select(item => item.ComparisonSubjectKey).Distinct().Count() == 1 &&
                   sources.Select(item => item.FieldIdentity.Scope).Distinct().Count() == 1;
        }

        private static bool TargetsAreEligibleForPropagation(
            IReadOnlyCollection<MoneyOccurrence> targets,
            ValueCategory sourceValueCategory,
            Scope sourceScope)
        {
            return targets.All(item =>
                item.FieldIdentity.ValueCategory == sourceValueCategory &&
                IsSupportedValueCategory(item.FieldIdentity.ValueCategory) &&
                item.FieldIdentity.TaxBasis == TaxBasis.TaxIncluded &&
                item.FieldIdentity.Scope == sourceScope &&
                item.ValueCategoryConfidence == ClassificationConfidence.High &&
                item.TaxBasisConfidence == ClassificationConfidence.High &&
                item.SubjectKind == SubjectKind.Unknown);
        }

        private static bool IsSupportedValueCategory(ValueCategory category)
        {
            return category == ValueCategory.AppraisedValue ||
                   category == ValueCategory.AppraisedNetValue;
        }

        private static bool HasBlockingBusinessField(
            string text,
            TextWindow sentence,
            TextWindow taxExcludedLabel,
            TextWindow taxIncludedLabel)
        {
            char[] residue = text.Substring(sentence.Start, sentence.Length).ToCharArray();
            Blank(residue, taxExcludedLabel.Start - sentence.Start, taxExcludedLabel.Length);
            Blank(residue, taxIncludedLabel.Start - sentence.Start, taxIncludedLabel.Length);

            ValueCategory ignoredCategory;
            string ignoredKeyword;
            return BusinessKeywordMatcher.TryClassifyValue(
                new string(residue),
                out ignoredCategory,
                out ignoredKeyword);
        }

        private static bool HasOnlySafePresentationText(
            string text,
            TextWindow sentence,
            TextWindow taxExcludedLabel,
            TextWindow taxIncludedLabel,
            IEnumerable<MoneyOccurrence> occurrences,
            ValueCategory valueCategory)
        {
            int presentationStart = taxExcludedLabel.End;
            char[] residue = text.Substring(
                presentationStart,
                sentence.End - presentationStart).ToCharArray();
            Blank(residue, taxIncludedLabel.Start - presentationStart, taxIncludedLabel.Length);
            foreach (MoneyOccurrence occurrence in occurrences)
            {
                Blank(
                    residue,
                    occurrence.TextStartIndex - presentationStart,
                    occurrence.TextLength);
            }

            string remaining = new string(residue);
            foreach (string word in SafePresentationWords)
            {
                remaining = remaining.Replace(word, string.Empty);
            }

            if (valueCategory == ValueCategory.AppraisedNetValue)
            {
                foreach (string word in AppraisedNetValueSafePresentationWords)
                {
                    remaining = remaining.Replace(word, string.Empty);
                }
            }

            return remaining.All(IsSafePresentationCharacter);
        }

        private static bool IsSafePresentationCharacter(char character)
        {
            return char.IsWhiteSpace(character) ||
                   "，,、（）()【】[]《》<>：:'‘’\"“”￥¥".IndexOf(character) >= 0;
        }

        private static MoneyOccurrence WithPropagatedSubject(
            MoneyOccurrence target,
            MoneyOccurrence source)
        {
            List<string> evidence = target.SubjectEvidence.ToList();
            evidence.Add(
                string.Format(
                    "同一段落的明确成对税口径结构将已确认的 TaxExcluded {0} 资产主体传播至 TaxIncluded {0}；金额值未参与主体判断。",
                    source.FieldIdentity.ValueCategory));

            return new MoneyOccurrence(
                target.RawText,
                target.NormalizedAmountYuan,
                target.MoneyParseStatus,
                target.MoneyNumberFormat,
                target.MoneyUnit,
                target.ParseWarnings,
                target.FieldIdentity,
                target.Location,
                target.NearbyContext,
                target.ClassificationConfidence,
                target.ValueCategoryConfidence,
                target.TaxBasisConfidence,
                target.ScopeConfidence,
                target.ClassificationEvidence,
                source.ComparisonSubjectKey,
                source.SubjectConfidence,
                evidence,
                target.TextStartIndex,
                target.TextLength);
        }

        private static bool TryFindUniqueLabel(
            string text,
            TextWindow sentence,
            IEnumerable<string> labels,
            bool rejectNegatedOccurrence,
            out TextWindow match)
        {
            var matches = new List<TextWindow>();
            foreach (string label in labels)
            {
                int searchIndex = sentence.Start;
                while (searchIndex < sentence.End)
                {
                    int index = text.IndexOf(
                        label,
                        searchIndex,
                        sentence.End - searchIndex,
                        StringComparison.Ordinal);
                    if (index < 0)
                    {
                        break;
                    }

                    if (!rejectNegatedOccurrence || index == 0 || text[index - 1] != '不')
                    {
                        matches.Add(new TextWindow(index, label.Length));
                    }

                    searchIndex = index + label.Length;
                }
            }

            List<TextWindow> distinct = matches
                .GroupBy(item => item.Start)
                .Select(group => group.OrderByDescending(item => item.Length).First())
                .ToList();
            if (distinct.Count != 1)
            {
                match = default(TextWindow);
                return false;
            }

            match = distinct[0];
            return true;
        }

        private static IEnumerable<TextWindow> FindSentences(string text)
        {
            string source = text ?? string.Empty;
            int start = 0;
            for (int index = 0; index <= source.Length; index++)
            {
                if (index < source.Length && HardSentenceBoundaries.IndexOf(source[index]) < 0)
                {
                    continue;
                }

                if (index > start)
                {
                    yield return new TextWindow(start, index - start);
                }

                start = index + 1;
            }
        }

        private static void Blank(char[] characters, int start, int length)
        {
            if (start < 0 || length < 0 || start + length > characters.Length)
            {
                return;
            }

            for (int index = start; index < start + length; index++)
            {
                characters[index] = ' ';
            }
        }

        private struct TextWindow
        {
            public TextWindow(int start, int length)
            {
                Start = start;
                Length = length;
            }

            public int Start { get; }

            public int Length { get; }

            public int End => Start + Length;

            public bool Contains(int start, int length)
            {
                return start >= Start && start + length <= End;
            }
        }
    }
}
