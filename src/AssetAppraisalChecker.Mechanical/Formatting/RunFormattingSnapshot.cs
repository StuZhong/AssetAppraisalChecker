using System;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class RunFormattingSnapshot
    {
        public RunFormattingSnapshot(
            int runIndex,
            string text,
            string characterStyleId,
            FormattingResolutionStatus characterStyleResolution,
            FormattingValue<string> fontAscii,
            FormattingValue<string> fontHighAnsi,
            FormattingValue<string> fontEastAsia,
            FormattingValue<string> fontComplexScript,
            FormattingValue<decimal> fontSize,
            FormattingValue<decimal> fontSizeComplexScript,
            FormattingValue<bool> bold,
            FormattingValue<bool> boldComplexScript,
            FormattingValue<bool> italic,
            FormattingValue<bool> italicComplexScript,
            FormattingValue<string> underline,
            FormattingValue<bool> strike,
            FormattingValue<bool> doubleStrike,
            FormattingValue<string> color,
            FormattingValue<string> highlight,
            FormattingValue<string> verticalAlignment,
            FormattingValue<bool> hidden,
            FormattingValue<string> language,
            bool isHyperlink,
            bool isField)
        {
            if (runIndex < 0) throw new ArgumentOutOfRangeException(nameof(runIndex));
            RunIndex = runIndex;
            Text = text ?? string.Empty;
            CharacterStyleId = characterStyleId ?? string.Empty;
            CharacterStyleResolution = characterStyleResolution;
            EffectiveFontAscii = fontAscii ?? throw new ArgumentNullException(nameof(fontAscii));
            EffectiveFontHighAnsi = fontHighAnsi ?? throw new ArgumentNullException(nameof(fontHighAnsi));
            EffectiveFontEastAsia = fontEastAsia ?? throw new ArgumentNullException(nameof(fontEastAsia));
            EffectiveFontComplexScript = fontComplexScript ?? throw new ArgumentNullException(nameof(fontComplexScript));
            EffectiveFontSize = fontSize ?? throw new ArgumentNullException(nameof(fontSize));
            EffectiveFontSizeComplexScript = fontSizeComplexScript ?? throw new ArgumentNullException(nameof(fontSizeComplexScript));
            Bold = bold ?? throw new ArgumentNullException(nameof(bold));
            BoldComplexScript = boldComplexScript ?? throw new ArgumentNullException(nameof(boldComplexScript));
            Italic = italic ?? throw new ArgumentNullException(nameof(italic));
            ItalicComplexScript = italicComplexScript ?? throw new ArgumentNullException(nameof(italicComplexScript));
            Underline = underline ?? throw new ArgumentNullException(nameof(underline));
            Strike = strike ?? throw new ArgumentNullException(nameof(strike));
            DoubleStrike = doubleStrike ?? throw new ArgumentNullException(nameof(doubleStrike));
            Color = color ?? throw new ArgumentNullException(nameof(color));
            Highlight = highlight ?? throw new ArgumentNullException(nameof(highlight));
            VerticalAlignment = verticalAlignment ?? throw new ArgumentNullException(nameof(verticalAlignment));
            Hidden = hidden ?? throw new ArgumentNullException(nameof(hidden));
            Language = language ?? throw new ArgumentNullException(nameof(language));
            IsHyperlink = isHyperlink;
            IsField = isField;
        }

        public int RunIndex { get; }
        public string Text { get; }
        public string CharacterStyleId { get; }
        public FormattingResolutionStatus CharacterStyleResolution { get; }
        public FormattingValue<string> EffectiveFontAscii { get; }
        public FormattingValue<string> EffectiveFontHighAnsi { get; }
        public FormattingValue<string> EffectiveFontEastAsia { get; }
        public FormattingValue<string> EffectiveFontComplexScript { get; }
        public FormattingValue<decimal> EffectiveFontSize { get; }
        public FormattingValue<decimal> EffectiveFontSizeComplexScript { get; }
        public FormattingValue<bool> Bold { get; }
        public FormattingValue<bool> BoldComplexScript { get; }
        public FormattingValue<bool> Italic { get; }
        public FormattingValue<bool> ItalicComplexScript { get; }
        public FormattingValue<string> Underline { get; }
        public FormattingValue<bool> Strike { get; }
        public FormattingValue<bool> DoubleStrike { get; }
        public FormattingValue<string> Color { get; }
        public FormattingValue<string> Highlight { get; }
        public FormattingValue<string> VerticalAlignment { get; }
        public FormattingValue<bool> Hidden { get; }
        public FormattingValue<string> Language { get; }
        public bool IsHyperlink { get; }
        public bool IsField { get; }
    }
}
