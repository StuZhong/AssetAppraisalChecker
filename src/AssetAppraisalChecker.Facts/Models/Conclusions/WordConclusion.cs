using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models.Conclusions
{
    public sealed class WordConclusion
    {
        public WordConclusion(string sourceDocument, WordConclusionTarget target,
            DocumentLocation headingLocation, int tableIndex, IEnumerable<WordConclusionAmount> amounts)
        {
            SourceDocument = sourceDocument;
            Target = target;
            HeadingLocation = headingLocation;
            TableIndex = tableIndex;
            Amounts = new ReadOnlyCollection<WordConclusionAmount>(amounts.ToList());
        }

        public string SourceDocument { get; }
        public WordConclusionTarget Target { get; }
        public DocumentLocation HeadingLocation { get; }
        public int TableIndex { get; }
        public IReadOnlyList<WordConclusionAmount> Amounts { get; }
    }
}
