using System;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    public sealed class MoneyCandidate
    {
        public MoneyCandidate(string rawText, int startIndex, int length)
            : this(rawText, startIndex, length, rawText)
        {
        }

        internal MoneyCandidate(string rawText, int startIndex, int length, string textForParsing)
        {
            if (startIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (rawText == null)
            {
                throw new ArgumentNullException(nameof(rawText));
            }

            if (rawText.Length != length)
            {
                throw new ArgumentException("候选文本长度必须与 Length 一致。", nameof(length));
            }

            RawText = rawText;
            StartIndex = startIndex;
            Length = length;
            TextForParsing = textForParsing ?? throw new ArgumentNullException(nameof(textForParsing));
        }

        public string RawText { get; }

        public int StartIndex { get; }

        public int Length { get; }

        internal string TextForParsing { get; }
    }
}
