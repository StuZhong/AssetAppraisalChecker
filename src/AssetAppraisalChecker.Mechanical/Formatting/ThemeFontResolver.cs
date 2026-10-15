using System;
using A = DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    internal sealed class ThemeFontResolver
    {
        private readonly ThemeMetadataSnapshot metadata;

        public ThemeFontResolver(ThemePart themePart)
        {
            A.FontScheme scheme = themePart?.Theme?.ThemeElements?.FontScheme;
            A.MajorFont major = scheme?.MajorFont;
            A.MinorFont minor = scheme?.MinorFont;
            metadata = new ThemeMetadataSnapshot(
                scheme != null,
                major?.LatinFont?.Typeface?.Value,
                major?.EastAsianFont?.Typeface?.Value,
                major?.ComplexScriptFont?.Typeface?.Value,
                minor?.LatinFont?.Typeface?.Value,
                minor?.EastAsianFont?.Typeface?.Value,
                minor?.ComplexScriptFont?.Typeface?.Value);
        }

        public ThemeMetadataSnapshot Metadata => metadata;

        public FormattingValue<string> Resolve(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return FormattingValue<string>.Unknown();
            }

            string typeface = GetTypeface(reference);
            return string.IsNullOrWhiteSpace(typeface)
                ? FormattingValue<string>.Unresolved(reference, FormattingSource.Theme)
                : FormattingValue<string>.Resolved(
                    typeface,
                    FormattingSource.Theme,
                    reference,
                    null,
                    reference);
        }

        private string GetTypeface(string reference)
        {
            switch (reference)
            {
                case "majorAscii":
                case "majorHAnsi":
                    return metadata.MajorLatin;
                case "majorEastAsia":
                    return metadata.MajorEastAsia;
                case "majorBidi":
                    return metadata.MajorComplexScript;
                case "minorAscii":
                case "minorHAnsi":
                    return metadata.MinorLatin;
                case "minorEastAsia":
                    return metadata.MinorEastAsia;
                case "minorBidi":
                    return metadata.MinorComplexScript;
                default:
                    return string.Empty;
            }
        }
    }
}
