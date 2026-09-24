using System;
using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Document
{
    internal static class NearbyContextBuilder
    {
        private const int ContextTextLimit = 250;

        public static IReadOnlyList<DocumentElement> Build(
            IReadOnlyList<ExtractedDocumentElement> extractedElements)
        {
            var previousTexts = new string[extractedElements.Count];
            var nextTexts = new string[extractedElements.Count];

            string nearestText = string.Empty;
            for (int index = 0; index < extractedElements.Count; index++)
            {
                previousTexts[index] = nearestText;
                if (!string.IsNullOrWhiteSpace(extractedElements[index].Text))
                {
                    nearestText = Limit(extractedElements[index].Text);
                }
            }

            nearestText = string.Empty;
            for (int index = extractedElements.Count - 1; index >= 0; index--)
            {
                nextTexts[index] = nearestText;
                if (!string.IsNullOrWhiteSpace(extractedElements[index].Text))
                {
                    nearestText = Limit(extractedElements[index].Text);
                }
            }

            var elements = new List<DocumentElement>(extractedElements.Count);
            int firstPageBoundaryIndex = FindFirstPageBoundaryIndex(extractedElements);
            for (int index = 0; index < extractedElements.Count; index++)
            {
                ExtractedDocumentElement extracted = extractedElements[index];
                var context = new DocumentNearbyContext(
                    previousTexts[index],
                    Limit(extracted.Text),
                    nextTexts[index]);
                var boundaryMetadata = new DocumentBoundaryMetadata(
                    extracted.ExplicitPageBreakCount,
                    extracted.SectionBreakKind,
                    GetFirstPageBoundaryRelation(
                        extractedElements,
                        firstPageBoundaryIndex,
                        index));

                elements.Add(new DocumentElement(
                    extracted.ElementType,
                    extracted.Text,
                    extracted.Location,
                    extracted.DocumentOrderIndex,
                    extracted.StyleId,
                    context,
                    boundaryMetadata));
            }

            return elements.AsReadOnly();
        }

        private static int FindFirstPageBoundaryIndex(
            IReadOnlyList<ExtractedDocumentElement> elements)
        {
            for (int index = 0; index < elements.Count; index++)
            {
                if (elements[index].ContainsExplicitPageBreak ||
                    elements[index].HasPageCreatingSectionBreak)
                {
                    return index;
                }
            }

            return -1;
        }

        private static FirstPageBoundaryRelation GetFirstPageBoundaryRelation(
            IReadOnlyList<ExtractedDocumentElement> elements,
            int firstBoundaryIndex,
            int elementIndex)
        {
            if (firstBoundaryIndex < 0)
            {
                return FirstPageBoundaryRelation.NoReliableBoundary;
            }

            if (elementIndex < firstBoundaryIndex)
            {
                return FirstPageBoundaryRelation.BeforeBoundary;
            }

            if (elementIndex > firstBoundaryIndex)
            {
                return FirstPageBoundaryRelation.AfterBoundary;
            }

            return elements[elementIndex].ContainsExplicitPageBreak
                ? FirstPageBoundaryRelation.ContainsBoundary
                : FirstPageBoundaryRelation.BeforeBoundary;
        }

        private static string Limit(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= ContextTextLimit)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, ContextTextLimit) + "…";
        }
    }
}
