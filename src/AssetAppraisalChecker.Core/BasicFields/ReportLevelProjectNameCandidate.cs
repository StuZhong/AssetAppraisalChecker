using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public enum ReportLevelProjectNameAuthorityRole
    {
        Cover,
        Summary,
        Body
    }

    public enum ReportLevelProjectNameExtractionPattern
    {
        Pattern1TwoParagraphDirect,
        Pattern2ThreeParagraphCoverWithBlankSeparator,
        Pattern3ThreeParagraphSummaryDirect,
        Pattern4TwoParagraphBodyWithLeadingLineBreak
    }

    /// <summary>
    /// A complete report-level project-name title block. Source paragraphs are
    /// retained individually because a multi-paragraph title cannot be explained
    /// accurately by pointing at only its final line.
    /// </summary>
    public sealed class ReportLevelProjectNameCandidate
    {
        public ReportLevelProjectNameCandidate(
            string assembledText,
            ReportLevelProjectNameAuthorityRole authorityRole,
            IEnumerable<DocumentElement> sourceParagraphs,
            DocumentElement anchor,
            ReportLevelProjectNameExtractionPattern extractionPattern,
            BasicFieldNormalizationResult normalization)
        {
            if (anchor == null)
            {
                throw new ArgumentNullException(nameof(anchor));
            }

            if (normalization == null)
            {
                throw new ArgumentNullException(nameof(normalization));
            }

            List<DocumentElement> paragraphs = (sourceParagraphs ??
                Enumerable.Empty<DocumentElement>()).ToList();
            if (paragraphs.Count < 2 || paragraphs.Count > 3 ||
                paragraphs.Any(item => item == null))
            {
                throw new ArgumentException(
                    "报告级项目名称必须保留二至三个有效源段落。",
                    nameof(sourceParagraphs));
            }

            AssembledText = assembledText ?? string.Empty;
            AuthorityRole = authorityRole;
            SourceParagraphs = new ReadOnlyCollection<DocumentElement>(paragraphs);
            Anchor = anchor;
            ExtractionPattern = extractionPattern;
            Normalization = normalization;
        }

        public string AssembledText { get; }

        public ReportLevelProjectNameAuthorityRole AuthorityRole { get; }

        public IReadOnlyList<DocumentElement> SourceParagraphs { get; }

        public DocumentElement Anchor { get; }

        public ReportLevelProjectNameExtractionPattern ExtractionPattern { get; }

        public BasicFieldNormalizationResult Normalization { get; }

        public bool NormalizationSuccess => Normalization.IsSuccess;

        public string CanonicalText =>
            Normalization.IsSuccess && Normalization.NormalizedValue != null
                ? Normalization.NormalizedValue.CanonicalValue
                : string.Empty;
    }
}
