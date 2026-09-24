using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Document
{
    internal sealed class ExtractedDocumentElement
    {
        public ExtractedDocumentElement(
            DocumentElementType elementType,
            string text,
            DocumentLocation location,
            int documentOrderIndex,
            string styleId,
            int explicitPageBreakCount,
            DocumentSectionBreakKind sectionBreakKind)
        {
            ElementType = elementType;
            Text = text ?? string.Empty;
            Location = location;
            DocumentOrderIndex = documentOrderIndex;
            StyleId = styleId ?? string.Empty;
            ExplicitPageBreakCount = explicitPageBreakCount;
            SectionBreakKind = sectionBreakKind;
        }

        public DocumentElementType ElementType { get; }

        public string Text { get; }

        public DocumentLocation Location { get; }

        public int DocumentOrderIndex { get; }

        public string StyleId { get; }

        public int ExplicitPageBreakCount { get; }

        public DocumentSectionBreakKind SectionBreakKind { get; }

        public bool ContainsExplicitPageBreak => ExplicitPageBreakCount > 0;

        public bool HasPageCreatingSectionBreak =>
            SectionBreakKind == DocumentSectionBreakKind.NextPage ||
            SectionBreakKind == DocumentSectionBreakKind.EvenPage ||
            SectionBreakKind == DocumentSectionBreakKind.OddPage;
    }
}
