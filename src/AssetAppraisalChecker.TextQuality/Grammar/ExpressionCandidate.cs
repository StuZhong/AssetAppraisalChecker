using System;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.TextQuality.Grammar
{
    public sealed class ExpressionCandidate
    {
        public ExpressionCandidate(
            string sourceDocument,
            SentenceSpan sentence,
            TextSpan evidenceSpan,
            string normalizedEvidence,
            ExpressionEvidence evidence)
        {
            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException("文档来源不能为空。", nameof(sourceDocument));
            }

            SourceDocument = sourceDocument.Trim();
            Sentence = sentence ?? throw new ArgumentNullException(nameof(sentence));
            EvidenceSpan = evidenceSpan ?? throw new ArgumentNullException(nameof(evidenceSpan));
            NormalizedEvidence = normalizedEvidence ?? string.Empty;
            Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
            StableIdentity = CreateStableIdentity();
        }

        public string SourceDocument { get; }
        public SentenceSpan Sentence { get; }
        public string OriginalSentence => Sentence.Text;
        public DocumentLocation Location => Sentence.Element.Location;
        public GrammarParagraphRole ParagraphRole => Sentence.ParagraphRole;
        public TextSpan EvidenceSpan { get; }
        public string NormalizedEvidence { get; }
        public ExpressionEvidence Evidence { get; }
        public string StableIdentity { get; }

        private string CreateStableIdentity()
        {
            DocumentLocation location = Location;
            return string.Join("|", new[]
            {
                SourceDocument,
                "Language",
                "Grammar." + Evidence.Kind,
                Evidence.RuleId,
                location.DocumentPart.ToString(),
                NullableIndex(location.ParagraphIndex),
                NullableIndex(location.TableIndex),
                NullableIndex(location.RowIndex),
                NullableIndex(location.CellIndex),
                Sentence.Span.Start + ":" + Sentence.Span.Length,
                NormalizedEvidence
            });
        }

        private static string NullableIndex(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "-";
        }
    }
}
