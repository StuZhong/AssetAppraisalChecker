using System;
using System.Collections.Generic;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    internal sealed class EffectiveStyleResolver
    {
        private readonly StyleCatalog catalog;
        private readonly ThemeFontResolver theme;
        private readonly OpenXmlElement defaultRunProperties;
        private readonly OpenXmlElement defaultParagraphProperties;

        public EffectiveStyleResolver(
            StyleCatalog catalog,
            ThemeFontResolver theme,
            DocDefaults defaults)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.theme = theme ?? throw new ArgumentNullException(nameof(theme));
            defaultRunProperties = defaults?.RunPropertiesDefault?.RunPropertiesBaseStyle;
            defaultParagraphProperties = defaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle;
            DefaultsSnapshot = new DocumentDefaultsSnapshot(
                defaultRunProperties != null,
                defaultParagraphProperties != null);
        }

        public DocumentDefaultsSnapshot DefaultsSnapshot { get; }

        public ParagraphResolution ResolveParagraph(Paragraph paragraph)
        {
            ParagraphProperties direct = paragraph?.ParagraphProperties;
            string styleId = direct?.ParagraphStyleId?.Val?.Value ?? string.Empty;
            StyleChainResolution chain = catalog.Resolve(styleId, StyleValues.Paragraph);
            var paragraphValues = new ParagraphFormattingAccumulator();
            var paragraphRunValues = new RunFormattingAccumulator(theme);

            paragraphValues.Apply(defaultParagraphProperties, FormattingSource.DocumentDefault);
            paragraphRunValues.Apply(defaultRunProperties, FormattingSource.DocumentDefault);
            if (chain.Status == FormattingResolutionStatus.Resolved)
            {
                foreach (Style style in chain.Styles)
                {
                    paragraphValues.Apply(style.StyleParagraphProperties, FormattingSource.ParagraphStyle);
                    paragraphRunValues.Apply(style.StyleRunProperties, FormattingSource.ParagraphStyle);
                }
            }
            else
            {
                paragraphValues.MarkUnresolved(chain.Message);
                paragraphRunValues.MarkUnresolved(chain.Message);
            }

            paragraphValues.Apply(direct, FormattingSource.DirectFormatting);
            paragraphRunValues.Apply(direct?.ParagraphMarkRunProperties, FormattingSource.DirectFormatting);
            return new ParagraphResolution(
                chain.Status == FormattingResolutionStatus.Resolved
                    ? chain.AppliedStyleId
                    : styleId,
                chain.Status,
                paragraphValues,
                paragraphRunValues);
        }

        public RunFormattingSnapshot ResolveRun(
            Run run,
            int runIndex,
            string text,
            ParagraphResolution paragraph,
            bool isHyperlink,
            bool isField)
        {
            RunProperties direct = run?.RunProperties;
            string characterStyleId = direct?.RunStyle?.Val?.Value ?? string.Empty;
            StyleChainResolution characterChain = catalog.Resolve(
                characterStyleId,
                StyleValues.Character);

            RunFormattingAccumulator values = paragraph.RunValues.Clone();
            if (characterChain.Status == FormattingResolutionStatus.Resolved)
            {
                foreach (Style style in characterChain.Styles)
                {
                    values.Apply(style.StyleRunProperties, FormattingSource.CharacterStyle);
                }
            }
            else
            {
                values.MarkUnresolved(characterChain.Message);
            }

            values.Apply(direct, FormattingSource.DirectFormatting);
            return values.CreateSnapshot(
                runIndex,
                text,
                characterChain.Status == FormattingResolutionStatus.Resolved
                    ? characterChain.AppliedStyleId
                    : characterStyleId,
                characterChain.Status,
                isHyperlink,
                isField);
        }
    }

    internal sealed class ParagraphResolution
    {
        public ParagraphResolution(
            string styleId,
            FormattingResolutionStatus styleResolution,
            ParagraphFormattingAccumulator paragraphValues,
            RunFormattingAccumulator runValues)
        {
            StyleId = styleId ?? string.Empty;
            StyleResolution = styleResolution;
            ParagraphValues = paragraphValues;
            RunValues = runValues;
        }

        public string StyleId { get; }
        public FormattingResolutionStatus StyleResolution { get; }
        public ParagraphFormattingAccumulator ParagraphValues { get; }
        public RunFormattingAccumulator RunValues { get; }
    }

    internal sealed class ParagraphFormattingAccumulator
    {
        private FormattingValue<string> alignment = FormattingValue<string>.Unknown();
        private FormattingValue<decimal> leftIndent = FormattingValue<decimal>.Unknown();
        private FormattingValue<decimal> rightIndent = FormattingValue<decimal>.Unknown();
        private FormattingValue<decimal> firstLineIndent = FormattingValue<decimal>.Unknown();
        private FormattingValue<decimal> hangingIndent = FormattingValue<decimal>.Unknown();
        private FormattingValue<decimal> spacingBefore = FormattingValue<decimal>.Unknown();
        private FormattingValue<decimal> spacingAfter = FormattingValue<decimal>.Unknown();
        private FormattingValue<decimal> lineSpacing = FormattingValue<decimal>.Unknown();
        private FormattingValue<string> lineRule = FormattingValue<string>.Unknown();
        private FormattingValue<int> numberingId = FormattingValue<int>.Unknown();
        private FormattingValue<int> numberingLevel = FormattingValue<int>.Unknown();
        private FormattingValue<bool> keepNext = FormattingValue<bool>.Unknown();
        private FormattingValue<bool> keepLines = FormattingValue<bool>.Unknown();
        private FormattingValue<bool> pageBreakBefore = FormattingValue<bool>.Unknown();

        public void Apply(OpenXmlElement properties, FormattingSource source)
        {
            if (properties == null) return;

            Justification justification = properties.GetFirstChild<Justification>();
            if (justification?.Val != null)
            {
                alignment = FormattingValue<string>.Resolved(
                    justification.Val.InnerText, source, justification.Val.InnerText);
            }

            Indentation indentation = properties.GetFirstChild<Indentation>();
            if (indentation != null)
            {
                ApplyTwips(indentation.Left?.Value, source, ref leftIndent);
                ApplyTwips(indentation.Right?.Value, source, ref rightIndent);
                ApplyTwips(indentation.FirstLine?.Value, source, ref firstLineIndent);
                ApplyTwips(indentation.Hanging?.Value, source, ref hangingIndent);
                ApplyCharacters(indentation.LeftChars?.Value, source, ref leftIndent);
                ApplyCharacters(indentation.RightChars?.Value, source, ref rightIndent);
                ApplyCharacters(indentation.FirstLineChars?.Value, source, ref firstLineIndent);
                ApplyCharacters(indentation.HangingChars?.Value, source, ref hangingIndent);
            }

            SpacingBetweenLines spacing = properties.GetFirstChild<SpacingBetweenLines>();
            if (spacing != null)
            {
                ApplyTwips(spacing.Before?.Value, source, ref spacingBefore);
                ApplyTwips(spacing.After?.Value, source, ref spacingAfter);
                ApplyLines(spacing.BeforeLines?.Value, source, ref spacingBefore);
                ApplyLines(spacing.AfterLines?.Value, source, ref spacingAfter);
                if (IsEnabled(spacing.BeforeAutoSpacing))
                {
                    spacingBefore = FormattingValue<decimal>.Unresolved(
                        "beforeAutospacing", source);
                }
                if (IsEnabled(spacing.AfterAutoSpacing))
                {
                    spacingAfter = FormattingValue<decimal>.Unresolved(
                        "afterAutospacing", source);
                }
                ApplyLineSpacing(spacing, source);
            }

            NumberingProperties numbering = properties.GetFirstChild<NumberingProperties>();
            if (numbering?.NumberingId?.Val != null)
            {
                numberingId = FormattingValue<int>.Resolved(
                    numbering.NumberingId.Val.Value, source,
                    numbering.NumberingId.Val.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (numbering?.NumberingLevelReference?.Val != null)
            {
                numberingLevel = FormattingValue<int>.Resolved(
                    numbering.NumberingLevelReference.Val.Value, source,
                    numbering.NumberingLevelReference.Val.Value.ToString(CultureInfo.InvariantCulture));
            }

            ApplyToggle(properties.GetFirstChild<KeepNext>(), source, ref keepNext);
            ApplyToggle(properties.GetFirstChild<KeepLines>(), source, ref keepLines);
            ApplyToggle(properties.GetFirstChild<PageBreakBefore>(), source, ref pageBreakBefore);
        }

        public void MarkUnresolved(string reference)
        {
            alignment = Unresolved(alignment, reference);
            leftIndent = Unresolved(leftIndent, reference);
            rightIndent = Unresolved(rightIndent, reference);
            firstLineIndent = Unresolved(firstLineIndent, reference);
            hangingIndent = Unresolved(hangingIndent, reference);
            spacingBefore = Unresolved(spacingBefore, reference);
            spacingAfter = Unresolved(spacingAfter, reference);
            lineSpacing = Unresolved(lineSpacing, reference);
            lineRule = Unresolved(lineRule, reference);
            numberingId = Unresolved(numberingId, reference);
            numberingLevel = Unresolved(numberingLevel, reference);
            keepNext = Unresolved(keepNext, reference);
            keepLines = Unresolved(keepLines, reference);
            pageBreakBefore = Unresolved(pageBreakBefore, reference);
        }

        public ParagraphFormattingSnapshot CreateSnapshot(
            FormattingParagraphLocation location,
            string textPreview,
            string styleId,
            FormattingResolutionStatus styleResolution,
            TableFormattingContext tableContext,
            IEnumerable<RunFormattingSnapshot> runs)
        {
            return new ParagraphFormattingSnapshot(
                location, textPreview, styleId, styleResolution,
                alignment, leftIndent, rightIndent, firstLineIndent, hangingIndent,
                spacingBefore, spacingAfter, lineSpacing, lineRule,
                numberingId, numberingLevel, keepNext, keepLines, pageBreakBefore,
                tableContext, runs);
        }

        private void ApplyLineSpacing(SpacingBetweenLines spacing, FormattingSource source)
        {
            string rule = spacing.LineRule?.InnerText ?? "auto";
            if (spacing.LineRule != null)
            {
                lineRule = FormattingValue<string>.Resolved(rule, source, rule);
            }
            if (spacing.Line == null) return;

            if (spacing.LineRule == null)
            {
                lineRule = FormattingValue<string>.Resolved("auto", source, "auto");
            }

            decimal raw;
            if (!decimal.TryParse(spacing.Line.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out raw))
            {
                lineSpacing = FormattingValue<decimal>.Unresolved("Invalid line spacing: " + spacing.Line.Value);
                return;
            }

            bool automatic = string.Equals(rule, "auto", StringComparison.Ordinal);
            lineSpacing = FormattingValue<decimal>.Resolved(
                automatic ? raw / 240m : raw / 20m,
                source,
                spacing.Line.Value,
                automatic ? "LineMultiple" : "Points");
        }

        private static void ApplyTwips(
            string raw,
            FormattingSource source,
            ref FormattingValue<decimal> target)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            decimal twips;
            target = decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out twips)
                ? FormattingValue<decimal>.Resolved(twips, source, raw, "Twips")
                : FormattingValue<decimal>.Unresolved("Invalid twips: " + raw);
        }

        private static void ApplyCharacters(
            int? raw,
            FormattingSource source,
            ref FormattingValue<decimal> target)
        {
            if (!raw.HasValue) return;
            string rawText = raw.Value.ToString(CultureInfo.InvariantCulture);
            target = FormattingValue<decimal>.Resolved(raw.Value / 100m, source, rawText, "Characters");
        }

        private static void ApplyLines(
            int? raw,
            FormattingSource source,
            ref FormattingValue<decimal> target)
        {
            if (!raw.HasValue) return;
            string rawText = raw.Value.ToString(CultureInfo.InvariantCulture);
            target = FormattingValue<decimal>.Resolved(raw.Value / 100m, source, rawText, "Lines");
        }

        private static bool IsEnabled(OnOffValue value)
        {
            return value != null && value.Value;
        }

        private static void ApplyToggle<T>(
            T value,
            FormattingSource source,
            ref FormattingValue<bool> target)
            where T : OnOffType
        {
            if (value == null) return;
            target = FormattingValue<bool>.Resolved(
                value.Val == null || value.Val.Value,
                source,
                value.Val == null ? "true" : value.Val.Value.ToString());
        }

        private static FormattingValue<T> Unresolved<T>(FormattingValue<T> current, string reference)
        {
            return current.Source == FormattingSource.DirectFormatting
                ? current
                : FormattingValue<T>.Unresolved(reference);
        }
    }

    internal sealed class RunFormattingAccumulator
    {
        private readonly ThemeFontResolver theme;
        private FormattingValue<string> fontAscii = FormattingValue<string>.Unknown();
        private FormattingValue<string> fontHighAnsi = FormattingValue<string>.Unknown();
        private FormattingValue<string> fontEastAsia = FormattingValue<string>.Unknown();
        private FormattingValue<string> fontComplexScript = FormattingValue<string>.Unknown();
        private FormattingValue<decimal> fontSize = FormattingValue<decimal>.Unknown();
        private FormattingValue<decimal> fontSizeCs = FormattingValue<decimal>.Unknown();
        private FormattingValue<bool> bold = FormattingValue<bool>.Unknown();
        private FormattingValue<bool> boldCs = FormattingValue<bool>.Unknown();
        private FormattingValue<bool> italic = FormattingValue<bool>.Unknown();
        private FormattingValue<bool> italicCs = FormattingValue<bool>.Unknown();
        private FormattingValue<string> underline = FormattingValue<string>.Unknown();
        private FormattingValue<bool> strike = FormattingValue<bool>.Unknown();
        private FormattingValue<bool> doubleStrike = FormattingValue<bool>.Unknown();
        private FormattingValue<string> color = FormattingValue<string>.Unknown();
        private FormattingValue<string> highlight = FormattingValue<string>.Unknown();
        private FormattingValue<string> verticalAlignment = FormattingValue<string>.Unknown();
        private FormattingValue<bool> hidden = FormattingValue<bool>.Unknown();
        private FormattingValue<string> language = FormattingValue<string>.Unknown();

        public RunFormattingAccumulator(ThemeFontResolver theme)
        {
            this.theme = theme;
        }

        public RunFormattingAccumulator Clone()
        {
            return new RunFormattingAccumulator(theme)
            {
                fontAscii = fontAscii,
                fontHighAnsi = fontHighAnsi,
                fontEastAsia = fontEastAsia,
                fontComplexScript = fontComplexScript,
                fontSize = fontSize,
                fontSizeCs = fontSizeCs,
                bold = bold,
                boldCs = boldCs,
                italic = italic,
                italicCs = italicCs,
                underline = underline,
                strike = strike,
                doubleStrike = doubleStrike,
                color = color,
                highlight = highlight,
                verticalAlignment = verticalAlignment,
                hidden = hidden,
                language = language
            };
        }

        public void Apply(OpenXmlElement properties, FormattingSource source)
        {
            if (properties == null) return;
            RunFonts fonts = properties.GetFirstChild<RunFonts>();
            if (fonts != null)
            {
                ApplyFont(fonts.Ascii?.Value, fonts.AsciiTheme?.InnerText, source, ref fontAscii);
                ApplyFont(fonts.HighAnsi?.Value, fonts.HighAnsiTheme?.InnerText, source, ref fontHighAnsi);
                ApplyFont(fonts.EastAsia?.Value, fonts.EastAsiaTheme?.InnerText, source, ref fontEastAsia);
                ApplyFont(fonts.ComplexScript?.Value, fonts.ComplexScriptTheme?.InnerText, source, ref fontComplexScript);
            }

            ApplyHalfPoints(properties.GetFirstChild<FontSize>()?.Val?.Value, source, ref fontSize);
            ApplyHalfPoints(properties.GetFirstChild<FontSizeComplexScript>()?.Val?.Value, source, ref fontSizeCs);
            ApplyToggle(properties.GetFirstChild<Bold>(), source, ref bold);
            ApplyToggle(properties.GetFirstChild<BoldComplexScript>(), source, ref boldCs);
            ApplyToggle(properties.GetFirstChild<Italic>(), source, ref italic);
            ApplyToggle(properties.GetFirstChild<ItalicComplexScript>(), source, ref italicCs);
            ApplyToggle(properties.GetFirstChild<Strike>(), source, ref strike);
            ApplyToggle(properties.GetFirstChild<DoubleStrike>(), source, ref doubleStrike);
            ApplyToggle(properties.GetFirstChild<Vanish>(), source, ref hidden);

            Underline underlineElement = properties.GetFirstChild<Underline>();
            if (underlineElement != null)
            {
                string value = underlineElement.Val?.InnerText ?? "single";
                underline = FormattingValue<string>.Resolved(value, source, value);
            }

            Color colorElement = properties.GetFirstChild<Color>();
            if (colorElement != null)
            {
                if (colorElement.Val != null)
                {
                    color = FormattingValue<string>.Resolved(
                        colorElement.Val.Value, source, colorElement.Val.Value);
                }
                else if (colorElement.ThemeColor != null)
                {
                    color = FormattingValue<string>.Unresolved(
                        "ThemeColor:" + colorElement.ThemeColor.InnerText, source);
                }
            }

            Highlight highlightElement = properties.GetFirstChild<Highlight>();
            if (highlightElement?.Val != null)
            {
                string value = highlightElement.Val.InnerText;
                highlight = FormattingValue<string>.Resolved(value, source, value);
            }

            VerticalTextAlignment vertical = properties.GetFirstChild<VerticalTextAlignment>();
            if (vertical?.Val != null)
            {
                string value = vertical.Val.InnerText;
                verticalAlignment = FormattingValue<string>.Resolved(value, source, value);
            }

            Languages languages = properties.GetFirstChild<Languages>();
            if (languages != null)
            {
                var parts = new List<string>();
                if (languages.Val != null) parts.Add("val=" + languages.Val.Value);
                if (languages.EastAsia != null) parts.Add("eastAsia=" + languages.EastAsia.Value);
                if (languages.Bidi != null) parts.Add("bidi=" + languages.Bidi.Value);
                if (parts.Count > 0)
                {
                    string value = string.Join(";", parts);
                    language = FormattingValue<string>.Resolved(value, source, value);
                }
            }
        }

        public void MarkUnresolved(string reference)
        {
            fontAscii = Unresolved(fontAscii, reference);
            fontHighAnsi = Unresolved(fontHighAnsi, reference);
            fontEastAsia = Unresolved(fontEastAsia, reference);
            fontComplexScript = Unresolved(fontComplexScript, reference);
            fontSize = Unresolved(fontSize, reference);
            fontSizeCs = Unresolved(fontSizeCs, reference);
            bold = Unresolved(bold, reference);
            boldCs = Unresolved(boldCs, reference);
            italic = Unresolved(italic, reference);
            italicCs = Unresolved(italicCs, reference);
            underline = Unresolved(underline, reference);
            strike = Unresolved(strike, reference);
            doubleStrike = Unresolved(doubleStrike, reference);
            color = Unresolved(color, reference);
            highlight = Unresolved(highlight, reference);
            verticalAlignment = Unresolved(verticalAlignment, reference);
            hidden = Unresolved(hidden, reference);
            language = Unresolved(language, reference);
        }

        public RunFormattingSnapshot CreateSnapshot(
            int runIndex,
            string text,
            string characterStyleId,
            FormattingResolutionStatus characterStyleResolution,
            bool isHyperlink,
            bool isField)
        {
            return new RunFormattingSnapshot(
                runIndex, text, characterStyleId, characterStyleResolution,
                fontAscii, fontHighAnsi, fontEastAsia, fontComplexScript,
                fontSize, fontSizeCs, bold, boldCs, italic, italicCs,
                underline, strike, doubleStrike, color, highlight,
                verticalAlignment, hidden, language, isHyperlink, isField);
        }

        private void ApplyFont(
            string explicitName,
            string themeReference,
            FormattingSource source,
            ref FormattingValue<string> target)
        {
            if (!string.IsNullOrWhiteSpace(themeReference))
            {
                target = theme.Resolve(themeReference);
                return;
            }

            if (!string.IsNullOrWhiteSpace(explicitName))
            {
                target = FormattingValue<string>.Resolved(explicitName, source, explicitName);
            }
        }

        private static void ApplyHalfPoints(
            string raw,
            FormattingSource source,
            ref FormattingValue<decimal> target)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            decimal halfPoints;
            target = decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out halfPoints)
                ? FormattingValue<decimal>.Resolved(halfPoints / 2m, source, raw, "Points")
                : FormattingValue<decimal>.Unresolved("Invalid half-points: " + raw);
        }

        private static void ApplyToggle<T>(
            T value,
            FormattingSource source,
            ref FormattingValue<bool> target)
            where T : OnOffType
        {
            if (value == null) return;
            target = FormattingValue<bool>.Resolved(
                value.Val == null || value.Val.Value,
                source,
                value.Val == null ? "true" : value.Val.Value.ToString());
        }

        private static FormattingValue<T> Unresolved<T>(FormattingValue<T> current, string reference)
        {
            return current.Source == FormattingSource.DirectFormatting
                ? current
                : FormattingValue<T>.Unresolved(reference);
        }
    }
}
