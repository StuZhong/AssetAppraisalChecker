using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class ParagraphFormattingSnapshot
    {
        public ParagraphFormattingSnapshot(
            FormattingParagraphLocation location,
            string textPreview,
            string styleId,
            FormattingResolutionStatus styleResolution,
            FormattingValue<string> alignment,
            FormattingValue<decimal> leftIndent,
            FormattingValue<decimal> rightIndent,
            FormattingValue<decimal> firstLineIndent,
            FormattingValue<decimal> hangingIndent,
            FormattingValue<decimal> spacingBefore,
            FormattingValue<decimal> spacingAfter,
            FormattingValue<decimal> lineSpacing,
            FormattingValue<string> lineRule,
            FormattingValue<int> numberingId,
            FormattingValue<int> numberingLevel,
            FormattingValue<bool> keepNext,
            FormattingValue<bool> keepLines,
            FormattingValue<bool> pageBreakBefore,
            TableFormattingContext tableContext,
            IEnumerable<RunFormattingSnapshot> runs)
        {
            Location = location ?? throw new ArgumentNullException(nameof(location));
            TextPreview = textPreview ?? string.Empty;
            StyleId = styleId ?? string.Empty;
            StyleResolution = styleResolution;
            EffectiveAlignment = alignment ?? throw new ArgumentNullException(nameof(alignment));
            EffectiveLeftIndent = leftIndent ?? throw new ArgumentNullException(nameof(leftIndent));
            EffectiveRightIndent = rightIndent ?? throw new ArgumentNullException(nameof(rightIndent));
            EffectiveFirstLineIndent = firstLineIndent ?? throw new ArgumentNullException(nameof(firstLineIndent));
            EffectiveHangingIndent = hangingIndent ?? throw new ArgumentNullException(nameof(hangingIndent));
            EffectiveSpacingBefore = spacingBefore ?? throw new ArgumentNullException(nameof(spacingBefore));
            EffectiveSpacingAfter = spacingAfter ?? throw new ArgumentNullException(nameof(spacingAfter));
            EffectiveLineSpacing = lineSpacing ?? throw new ArgumentNullException(nameof(lineSpacing));
            EffectiveLineRule = lineRule ?? throw new ArgumentNullException(nameof(lineRule));
            NumberingId = numberingId ?? throw new ArgumentNullException(nameof(numberingId));
            NumberingLevel = numberingLevel ?? throw new ArgumentNullException(nameof(numberingLevel));
            KeepNext = keepNext ?? throw new ArgumentNullException(nameof(keepNext));
            KeepLines = keepLines ?? throw new ArgumentNullException(nameof(keepLines));
            PageBreakBefore = pageBreakBefore ?? throw new ArgumentNullException(nameof(pageBreakBefore));
            TableContext = tableContext;
            Runs = new ReadOnlyCollection<RunFormattingSnapshot>(
                (runs ?? Enumerable.Empty<RunFormattingSnapshot>()).ToList());
        }

        public FormattingParagraphLocation Location { get; }
        public string TextPreview { get; }
        public string StyleId { get; }
        public FormattingResolutionStatus StyleResolution { get; }
        public FormattingValue<string> EffectiveAlignment { get; }
        public FormattingValue<decimal> EffectiveLeftIndent { get; }
        public FormattingValue<decimal> EffectiveRightIndent { get; }
        public FormattingValue<decimal> EffectiveFirstLineIndent { get; }
        public FormattingValue<decimal> EffectiveHangingIndent { get; }
        public FormattingValue<decimal> EffectiveSpacingBefore { get; }
        public FormattingValue<decimal> EffectiveSpacingAfter { get; }
        public FormattingValue<decimal> EffectiveLineSpacing { get; }
        public FormattingValue<string> EffectiveLineRule { get; }
        public FormattingValue<int> NumberingId { get; }
        public FormattingValue<int> NumberingLevel { get; }
        public FormattingValue<bool> KeepNext { get; }
        public FormattingValue<bool> KeepLines { get; }
        public FormattingValue<bool> PageBreakBefore { get; }
        public bool IsInTable => Location.IsInTable;
        public TableFormattingContext TableContext { get; }
        public IReadOnlyList<RunFormattingSnapshot> Runs { get; }
    }
}
