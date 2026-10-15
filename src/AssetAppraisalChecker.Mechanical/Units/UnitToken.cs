using System;

namespace AssetAppraisalChecker.Mechanical.Units
{
    public sealed class UnitToken
    {
        public UnitToken(
            string originalText,
            string canonicalUnit,
            UnitFamily family,
            decimal scale)
        {
            if (string.IsNullOrWhiteSpace(originalText))
            {
                throw new ArgumentException("单位原文不能为空。", nameof(originalText));
            }

            if (string.IsNullOrWhiteSpace(canonicalUnit))
            {
                throw new ArgumentException("标准单位不能为空。", nameof(canonicalUnit));
            }

            if (scale <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), "单位比例必须大于零。");
            }

            OriginalText = originalText;
            CanonicalUnit = canonicalUnit;
            Family = family;
            Scale = scale;
        }

        public string OriginalText { get; }

        public string CanonicalUnit { get; }

        public UnitFamily Family { get; }

        public decimal Scale { get; }
    }
}
