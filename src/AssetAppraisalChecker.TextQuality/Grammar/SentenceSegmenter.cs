using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.TextQuality.Grammar
{
    public sealed class SentenceSegmenter
    {
        public IReadOnlyList<SentenceSpan> Segment(
            DocumentElement element,
            GrammarParagraphRole paragraphRole,
            ProtectedTermRegistry protectedTerms)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (protectedTerms == null) throw new ArgumentNullException(nameof(protectedTerms));

            string text = element.Text ?? string.Empty;
            var result = new List<SentenceSpan>();
            int start = 0;
            int bracketDepth = 0;
            bool inChineseQuote = false;
            bool inBookTitle = false;

            for (int index = 0; index < text.Length; index++)
            {
                char value = text[index];
                if (value == '“') inChineseQuote = true;
                else if (value == '”') inChineseQuote = false;
                else if (value == '《') inBookTitle = true;
                else if (value == '》') inBookTitle = false;
                else if (value == '（' || value == '(' || value == '【' || value == '[') bracketDepth++;
                else if ((value == '）' || value == ')' || value == '】' || value == ']') && bracketDepth > 0) bracketDepth--;

                bool delimiter = value == '。' || value == '！' || value == '？' || value == '；';
                if (!delimiter || bracketDepth > 0 || inChineseQuote || inBookTitle ||
                    protectedTerms.IsProtected(element, new TextSpan(index, 1)))
                {
                    continue;
                }

                Add(result, element, text, start, index - start + 1, paragraphRole);
                start = index + 1;
            }

            Add(result, element, text, start, text.Length - start, paragraphRole);
            return new ReadOnlyCollection<SentenceSpan>(result);
        }

        private static void Add(
            ICollection<SentenceSpan> target,
            DocumentElement element,
            string source,
            int start,
            int length,
            GrammarParagraphRole role)
        {
            if (length <= 0) return;
            int actualStart = start;
            int end = start + length;
            while (actualStart < end && char.IsWhiteSpace(source[actualStart])) actualStart++;
            while (end > actualStart && char.IsWhiteSpace(source[end - 1])) end--;
            if (end <= actualStart) return;
            target.Add(new SentenceSpan(
                element,
                source.Substring(actualStart, end - actualStart),
                new TextSpan(actualStart, end - actualStart),
                role));
        }
    }
}
