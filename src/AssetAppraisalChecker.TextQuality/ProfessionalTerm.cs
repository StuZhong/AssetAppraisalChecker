using System;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class ProfessionalTerm
    {
        public ProfessionalTerm(string value, ProfessionalTermCategory category)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("专业术语不能为空。", nameof(value));
            }

            Value = value.Trim();
            Category = category;
        }

        public string Value { get; }

        public ProfessionalTermCategory Category { get; }
    }
}
