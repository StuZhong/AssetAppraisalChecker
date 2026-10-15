using System;

namespace AssetAppraisalChecker.Mechanical.Units
{
    public sealed class UnitExpression
    {
        private UnitExpression(
            string originalText,
            UnitToken numerator,
            UnitToken denominator)
        {
            OriginalText = originalText ?? string.Empty;
            Numerator = numerator ?? throw new ArgumentNullException(nameof(numerator));
            Denominator = denominator;
            IsCompound = denominator != null;
            Family = IsCompound ? UnitFamily.Unknown : numerator.Family;
            CanonicalUnit = numerator.CanonicalUnit;
            Scale = numerator.Scale;
            CanonicalExpression = IsCompound
                ? numerator.CanonicalUnit + "/" + denominator.CanonicalUnit
                : numerator.CanonicalUnit;
        }

        public string OriginalText { get; }

        public string CanonicalExpression { get; }

        public UnitFamily Family { get; }

        public string CanonicalUnit { get; }

        public decimal Scale { get; }

        public bool IsCompound { get; }

        public UnitToken Numerator { get; }

        public UnitToken Denominator { get; }

        public UnitFamily NumeratorFamily => Numerator.Family;

        public UnitFamily DenominatorFamily => Denominator?.Family ?? UnitFamily.Unknown;

        public static UnitExpression Atomic(UnitToken token)
        {
            return new UnitExpression(token?.OriginalText, token, null);
        }

        public static UnitExpression Compound(
            string originalText,
            UnitToken numerator,
            UnitToken denominator)
        {
            return new UnitExpression(originalText, numerator, denominator);
        }
    }
}
