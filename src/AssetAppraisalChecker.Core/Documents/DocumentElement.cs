using System;
using System.Collections.Generic;

namespace AssetAppraisalChecker.Core.Documents
{
    public sealed class DocumentElement
    {
        public DocumentElement(
            DocumentElementType elementType,
            string text,
            DocumentLocation location,
            int documentOrderIndex,
            string styleId,
            DocumentNearbyContext nearbyContext)
            : this(
                elementType,
                text,
                location,
                documentOrderIndex,
                styleId,
                nearbyContext,
                DocumentBoundaryMetadata.Unknown)
        {
        }

        public DocumentElement(
            DocumentElementType elementType,
            string text,
            DocumentLocation location,
            int documentOrderIndex,
            string styleId,
            DocumentNearbyContext nearbyContext,
            DocumentBoundaryMetadata boundaryMetadata)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (documentOrderIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(documentOrderIndex),
                    "文档顺序索引不能为负数。");
            }

            ElementType = elementType;
            Text = text ?? string.Empty;
            Location = location;
            DocumentOrderIndex = documentOrderIndex;
            StyleId = styleId ?? string.Empty;
            NearbyContext = nearbyContext ?? new DocumentNearbyContext(string.Empty, Text, string.Empty);
            BoundaryMetadata = boundaryMetadata ?? DocumentBoundaryMetadata.Unknown;
        }

        public DocumentElementType ElementType { get; }

        public string Text { get; }

        public DocumentLocation Location { get; }

        public int DocumentOrderIndex { get; }

        public IReadOnlyList<string> HeadingPath => Location.HeadingPath;

        public string StyleId { get; }

        public DocumentNearbyContext NearbyContext { get; }

        public DocumentBoundaryMetadata BoundaryMetadata { get; }
    }
}
