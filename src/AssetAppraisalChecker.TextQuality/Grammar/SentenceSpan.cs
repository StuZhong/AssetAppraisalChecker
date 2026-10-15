using System;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.TextQuality.Grammar
{
    public sealed class SentenceSpan
    {
        public SentenceSpan(
            DocumentElement element,
            string text,
            TextSpan span,
            GrammarParagraphRole paragraphRole)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("句子文本不能为空。", nameof(text));
            Text = text;
            Span = span ?? throw new ArgumentNullException(nameof(span));
            ParagraphRole = paragraphRole;
        }

        public DocumentElement Element { get; }
        public string Text { get; }
        public TextSpan Span { get; }
        public GrammarParagraphRole ParagraphRole { get; }
    }
}
