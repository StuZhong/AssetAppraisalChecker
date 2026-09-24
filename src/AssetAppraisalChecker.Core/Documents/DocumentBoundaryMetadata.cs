using System;

namespace AssetAppraisalChecker.Core.Documents
{
    public enum DocumentSectionBreakKind
    {
        None = 0,
        NextPage = 1,
        Continuous = 2,
        EvenPage = 3,
        OddPage = 4,
        NextColumn = 5,
        Unknown = 6
    }

    public enum FirstPageBoundaryRelation
    {
        NoReliableBoundary = 0,
        BeforeBoundary = 1,
        ContainsBoundary = 2,
        AfterBoundary = 3
    }

    /// <summary>
    /// Carries only page-boundary evidence explicitly stored in the DOCX package.
    /// It does not attempt to reproduce Word or WPS pagination.
    /// </summary>
    public sealed class DocumentBoundaryMetadata
    {
        public static readonly DocumentBoundaryMetadata Unknown =
            new DocumentBoundaryMetadata(
                0,
                DocumentSectionBreakKind.None,
                FirstPageBoundaryRelation.NoReliableBoundary);

        public DocumentBoundaryMetadata(
            int explicitPageBreakCount,
            DocumentSectionBreakKind sectionBreakKind,
            FirstPageBoundaryRelation firstPageRelation)
        {
            if (explicitPageBreakCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(explicitPageBreakCount));
            }

            ExplicitPageBreakCount = explicitPageBreakCount;
            SectionBreakKind = sectionBreakKind;
            FirstPageRelation = firstPageRelation;
        }

        public int ExplicitPageBreakCount { get; }

        public bool ContainsExplicitPageBreak => ExplicitPageBreakCount > 0;

        public DocumentSectionBreakKind SectionBreakKind { get; }

        public bool HasPageCreatingSectionBreak =>
            SectionBreakKind == DocumentSectionBreakKind.NextPage ||
            SectionBreakKind == DocumentSectionBreakKind.EvenPage ||
            SectionBreakKind == DocumentSectionBreakKind.OddPage;

        public bool HasReliablePageBoundaryAfter => HasPageCreatingSectionBreak;

        public FirstPageBoundaryRelation FirstPageRelation { get; }

        public bool DocumentHasReliableFirstPageBoundary =>
            FirstPageRelation != FirstPageBoundaryRelation.NoReliableBoundary;

        public bool IsBeforeFirstPageBoundary =>
            FirstPageRelation == FirstPageBoundaryRelation.BeforeBoundary;

        public bool ContainsFirstPageBoundary =>
            FirstPageRelation == FirstPageBoundaryRelation.ContainsBoundary;

        public bool IsAfterFirstPageBoundary =>
            FirstPageRelation == FirstPageBoundaryRelation.AfterBoundary;
    }
}
