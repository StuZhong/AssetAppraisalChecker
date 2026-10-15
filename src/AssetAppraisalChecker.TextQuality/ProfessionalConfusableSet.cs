using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class ProfessionalConfusableSet
    {
        private static readonly Lazy<ProfessionalConfusableSet> DefaultValue =
            new Lazy<ProfessionalConfusableSet>(() => new ProfessionalConfusableSet(
                "v2-text-quality-confusable/1.0",
                new[]
                {
                    new ProfessionalConfusableRule('曰', '日', "CF-DAY-001"),
                    new ProfessionalConfusableRule('曰', '目', "CF-EYE-001")
                }), true);

        public ProfessionalConfusableSet(
            string version,
            IEnumerable<ProfessionalConfusableRule> rules)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("形近字符规则版本不能为空。", nameof(version));
            }

            Version = version;
            Rules = new ReadOnlyCollection<ProfessionalConfusableRule>(
                (rules ?? throw new ArgumentNullException(nameof(rules))).ToList());
        }

        public static ProfessionalConfusableSet Default => DefaultValue.Value;

        public string Version { get; }

        public IReadOnlyList<ProfessionalConfusableRule> Rules { get; }

        public IReadOnlyList<string> FindLexiconCandidates(
            string observed,
            ProfessionalLexicon lexicon)
        {
            if (string.IsNullOrEmpty(observed))
            {
                return new string[0];
            }

            var candidates = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < observed.Length; index++)
            {
                foreach (ProfessionalConfusableRule rule in Rules.Where(item =>
                    item.ObservedCharacter == observed[index]))
                {
                    char[] characters = observed.ToCharArray();
                    characters[index] = rule.ExpectedCharacter;
                    string candidate = new string(characters);
                    if (lexicon.Contains(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            return new ReadOnlyCollection<string>(
                candidates.OrderBy(item => item, StringComparer.Ordinal).ToList());
        }

        public string GetRuleId(string observed, string expected)
        {
            if (observed == null || expected == null || observed.Length != expected.Length)
            {
                return string.Empty;
            }

            for (int index = 0; index < observed.Length; index++)
            {
                if (observed[index] == expected[index])
                {
                    continue;
                }

                ProfessionalConfusableRule rule = Rules.FirstOrDefault(item =>
                    item.ObservedCharacter == observed[index] &&
                    item.ExpectedCharacter == expected[index]);
                return rule?.RuleId ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
