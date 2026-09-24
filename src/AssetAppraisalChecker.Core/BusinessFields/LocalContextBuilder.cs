using System;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal static class LocalContextBuilder
    {
        private const string ClauseSeparators = "，,；;。！？!?\r\n";

        public static string Build(string text, MoneyCandidate candidate)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int start = candidate.StartIndex;
            while (start > 0 && ClauseSeparators.IndexOf(text[start - 1]) < 0)
            {
                start--;
            }

            int end = candidate.StartIndex + candidate.Length;
            while (end < text.Length && ClauseSeparators.IndexOf(text[end]) < 0)
            {
                end++;
            }

            return text.Substring(start, end - start).Trim();
        }
    }
}
