using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class MechanicalIssueOccurrence
    {
        public MechanicalIssueOccurrence(
            string sourceDocument,
            string rawText,
            DocumentLocation location,
            IEnumerable<string> evidence)
        {
            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException("文档来源不能为空。", nameof(sourceDocument));
            }

            SourceDocument = sourceDocument;
            RawText = rawText ?? string.Empty;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            Evidence = new ReadOnlyCollection<string>(
                (evidence ?? Enumerable.Empty<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList());
        }

        public string SourceDocument { get; }

        public string RawText { get; }

        public DocumentLocation Location { get; }

        public IReadOnlyList<string> Evidence { get; }
    }
}
