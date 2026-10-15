using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class DocumentFormattingSnapshot
    {
        public DocumentFormattingSnapshot(
            IEnumerable<ParagraphFormattingSnapshot> paragraphs,
            IEnumerable<TableFormattingContext> tables,
            IEnumerable<StyleMetadataSnapshot> styles,
            DocumentDefaultsSnapshot documentDefaults,
            ThemeMetadataSnapshot theme)
        {
            Paragraphs = new ReadOnlyCollection<ParagraphFormattingSnapshot>(
                (paragraphs ?? throw new ArgumentNullException(nameof(paragraphs))).ToList());
            Tables = new ReadOnlyCollection<TableFormattingContext>(
                (tables ?? throw new ArgumentNullException(nameof(tables))).ToList());
            Styles = new ReadOnlyCollection<StyleMetadataSnapshot>(
                (styles ?? throw new ArgumentNullException(nameof(styles))).ToList());
            DocumentDefaults = documentDefaults ?? throw new ArgumentNullException(nameof(documentDefaults));
            Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        }

        public IReadOnlyList<ParagraphFormattingSnapshot> Paragraphs { get; }
        public IReadOnlyList<TableFormattingContext> Tables { get; }
        public IReadOnlyList<StyleMetadataSnapshot> Styles { get; }
        public DocumentDefaultsSnapshot DocumentDefaults { get; }
        public ThemeMetadataSnapshot Theme { get; }
    }
}
