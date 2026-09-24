using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Document
{
    internal sealed class HeadingStyleResolver
    {
        private static readonly Regex HeadingPattern = new Regex(
            @"^(?:heading|标题)\s*([1-9])$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly IDictionary<string, int> _headingLevelsByStyleId;

        public HeadingStyleResolver(MainDocumentPart mainDocumentPart)
        {
            _headingLevelsByStyleId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            LoadStyleDefinitions(mainDocumentPart);
        }

        public int? GetHeadingLevel(Paragraph paragraph, out string styleId)
        {
            styleId = paragraph?.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(styleId))
            {
                return null;
            }

            int configuredLevel;
            if (_headingLevelsByStyleId.TryGetValue(styleId, out configuredLevel))
            {
                return configuredLevel;
            }

            return ParseHeadingLevel(styleId);
        }

        private void LoadStyleDefinitions(MainDocumentPart mainDocumentPart)
        {
            Styles styles = mainDocumentPart?.StyleDefinitionsPart?.Styles;
            if (styles == null)
            {
                return;
            }

            foreach (Style style in styles.Elements<Style>())
            {
                string styleId = style.StyleId?.Value;
                if (string.IsNullOrWhiteSpace(styleId))
                {
                    continue;
                }

                int? level = ParseHeadingLevel(styleId);
                if (!level.HasValue)
                {
                    level = ParseHeadingLevel(style.StyleName?.Val?.Value);
                }

                if (level.HasValue)
                {
                    _headingLevelsByStyleId[styleId] = level.Value;
                }
            }
        }

        private static int? ParseHeadingLevel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            Match match = HeadingPattern.Match(value.Trim());
            if (!match.Success)
            {
                return null;
            }

            return int.Parse(match.Groups[1].Value);
        }
    }
}
