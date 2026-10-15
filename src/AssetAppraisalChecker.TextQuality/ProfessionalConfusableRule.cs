using System;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class ProfessionalConfusableRule
    {
        public ProfessionalConfusableRule(char observedCharacter, char expectedCharacter, string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                throw new ArgumentException("形近字符规则标识不能为空。", nameof(ruleId));
            }

            ObservedCharacter = observedCharacter;
            ExpectedCharacter = expectedCharacter;
            RuleId = ruleId;
        }

        public char ObservedCharacter { get; }

        public char ExpectedCharacter { get; }

        public string RuleId { get; }
    }
}
