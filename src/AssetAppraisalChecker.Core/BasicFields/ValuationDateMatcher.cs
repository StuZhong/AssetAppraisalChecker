using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Core.BasicFields
{
    /// <summary>
    /// Finds only explicitly labelled valuation dates whose value starts at a
    /// deterministic boundary in the same text element. It intentionally does
    /// not search forward for an arbitrary date.
    /// </summary>
    internal static class ValuationDateMatcher
    {
        internal const int MaximumBridgeCharacterCount = 3;
        internal const int MaximumPaddingCharacterCount = 4;

        private static readonly string[] AllowedBridges =
        {
            "确定为",
            "设定为",
            "选取",
            "是"
        };

        private const string AuditedRangeToken = "起至";
        private const char AuditedRangeClosingCharacter = '止';

        private static readonly Regex LabelPattern = new Regex(
            "评估基准日期|评估基准日",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SupportedDatePattern = new Regex(
            @"(?:\d{4}年\d{1,2}月\d{1,2}日|" +
            @"\d{4}(?:-\d{1,2}-|/\d{1,2}/|\.\d{1,2}\.)\d{1,2})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly IReadOnlyList<ValuationDateMatch> NoMatches =
            new ReadOnlyCollection<ValuationDateMatch>(new ValuationDateMatch[0]);

        public static IReadOnlyList<ValuationDateMatch> FindMatches(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return NoMatches;
            }

            var results = new List<ValuationDateMatch>();
            foreach (Match labelMatch in LabelPattern.Matches(text))
            {
                ValuationDateMatch result;
                if (TryMatchAfterLabel(text, labelMatch, out result))
                {
                    results.Add(result);
                }
            }

            return new ReadOnlyCollection<ValuationDateMatch>(results);
        }

        internal static bool HasExactlyOneSupportedDateToken(
            string text,
            int startIndex,
            int length)
        {
            if (string.IsNullOrEmpty(text) ||
                startIndex < 0 ||
                length <= 0 ||
                startIndex > text.Length - length)
            {
                return false;
            }

            return SupportedDatePattern.Matches(
                text.Substring(startIndex, length)).Count == 1;
        }

        private static bool TryMatchAfterLabel(
            string text,
            Match labelMatch,
            out ValuationDateMatch result)
        {
            result = null;
            int labelEnd = labelMatch.Index + labelMatch.Length;
            int cursor = labelEnd;
            if (!SkipLimitedPadding(text, ref cursor))
            {
                return false;
            }

            string boundaryEvidence;
            if (cursor < text.Length && IsExistingSeparator(text[cursor]))
            {
                boundaryEvidence = "现有明确分隔符";
                cursor++;
            }
            else
            {
                string bridge = AllowedBridges.FirstOrDefault(item =>
                    StartsWithAt(text, cursor, item));
                if (bridge == null)
                {
                    boundaryEvidence = "标签后紧邻日期";
                }
                else
                {
                    if (bridge.Length > MaximumBridgeCharacterCount)
                    {
                        return false;
                    }

                    boundaryEvidence = "受限同子句连接词“" + bridge + "”";
                    cursor += bridge.Length;
                }
            }

            if (!SkipLimitedPadding(text, ref cursor) ||
                cursor - labelEnd > MaximumBridgeCharacterCount +
                    (MaximumPaddingCharacterCount * 2) + 1)
            {
                return false;
            }

            Match dateMatch = SupportedDatePattern.Match(text, cursor);
            if (!dateMatch.Success || dateMatch.Index != cursor)
            {
                return false;
            }

            string rawDate = dateMatch.Value;
            BasicFieldNormalizationResult normalization = BasicFieldNormalizer.Normalize(
                BasicFieldType.ValuationDate,
                rawDate);
            if (normalization.Status != BasicFieldParseStatus.Success)
            {
                return false;
            }

            if (IsCompleteAuditedRange(text, labelMatch.Index, dateMatch))
            {
                result = new ValuationDateMatch(
                    rawDate,
                    dateMatch.Index,
                    dateMatch.Length,
                    labelMatch.Value,
                    "已审计的完整起止日期范围结构");
                return true;
            }

            if (!HasReasonableDateEnd(text, dateMatch.Index + dateMatch.Length) ||
                HasAnotherDateInSameClause(text, labelMatch.Index, dateMatch))
            {
                return false;
            }

            result = new ValuationDateMatch(
                rawDate,
                dateMatch.Index,
                dateMatch.Length,
                labelMatch.Value,
                boundaryEvidence);
            return true;
        }

        private static bool IsCompleteAuditedRange(
            string text,
            int labelStart,
            Match firstDate)
        {
            int cursor = firstDate.Index + firstDate.Length;
            if (!SkipLimitedPadding(text, ref cursor) ||
                !StartsWithAt(text, cursor, AuditedRangeToken))
            {
                return false;
            }

            cursor += AuditedRangeToken.Length;
            if (!SkipLimitedPadding(text, ref cursor))
            {
                return false;
            }

            Match secondDate = SupportedDatePattern.Match(text, cursor);
            if (!secondDate.Success || secondDate.Index != cursor)
            {
                return false;
            }

            BasicFieldNormalizationResult secondNormalization =
                BasicFieldNormalizer.Normalize(
                    BasicFieldType.ValuationDate,
                    secondDate.Value);
            if (secondNormalization.Status != BasicFieldParseStatus.Success ||
                !HasSafeRangeEnd(text, secondDate.Index + secondDate.Length))
            {
                return false;
            }

            int clauseEnd = FindStrongBoundary(
                text,
                secondDate.Index + secondDate.Length);
            int clauseLength = clauseEnd - labelStart;
            return clauseLength > 0 &&
                   SupportedDatePattern.Matches(
                       text.Substring(labelStart, clauseLength)).Count == 2;
        }

        private static bool HasSafeRangeEnd(string text, int rangeEnd)
        {
            int cursor = rangeEnd;
            if (!SkipLimitedPadding(text, ref cursor))
            {
                return false;
            }

            if (cursor >= text.Length)
            {
                return true;
            }

            if (IsSafeRangeBoundary(text[cursor]))
            {
                return true;
            }

            // “止”仅是完整“Date1 + 起至 + Date2”范围结构的已审计收束字符；
            // 它不能成为普通单日期的通用结束边界。
            if (text[cursor] != AuditedRangeClosingCharacter)
            {
                return false;
            }

            cursor++;
            if (!SkipLimitedPadding(text, ref cursor))
            {
                return false;
            }

            return cursor >= text.Length || IsSafeRangeBoundary(text[cursor]);
        }

        private static bool IsSafeRangeBoundary(char value)
        {
            return value == '，' || value == ',' ||
                   value == '。' || value == '；' || value == ';' ||
                   value == '、' || value == ')' || value == '）';
        }

        private static bool SkipLimitedPadding(string text, ref int cursor)
        {
            int start = cursor;
            while (cursor < text.Length && IsHorizontalPadding(text[cursor]))
            {
                cursor++;
            }

            return cursor - start <= MaximumPaddingCharacterCount;
        }

        private static bool StartsWithAt(string text, int startIndex, string value)
        {
            return startIndex >= 0 &&
                   startIndex + value.Length <= text.Length &&
                   string.CompareOrdinal(text, startIndex, value, 0, value.Length) == 0;
        }

        private static bool IsHorizontalPadding(char value)
        {
            return value == ' ' || value == '\t' || value == '\u3000';
        }

        private static bool IsExistingSeparator(char value)
        {
            return value == ':' || value == '：' || value == '为';
        }

        private static bool HasReasonableDateEnd(string text, int dateEnd)
        {
            if (dateEnd >= text.Length)
            {
                return true;
            }

            char next = text[dateEnd];
            return char.IsWhiteSpace(next) ||
                   next == '，' || next == ',' ||
                   next == '。' || next == '；' || next == ';' ||
                   next == '、' || next == ')' || next == '）';
        }

        private static bool HasAnotherDateInSameClause(
            string text,
            int labelStart,
            Match selectedDate)
        {
            int clauseEnd = FindStrongBoundary(text, selectedDate.Index + selectedDate.Length);
            int clauseLength = clauseEnd - labelStart;
            if (clauseLength <= 0)
            {
                return false;
            }

            return SupportedDatePattern.Matches(
                text.Substring(labelStart, clauseLength)).Count != 1;
        }

        private static int FindStrongBoundary(string text, int startIndex)
        {
            for (int index = startIndex; index < text.Length; index++)
            {
                char value = text[index];
                if (value == '。' || value == '；' || value == ';' ||
                    value == '\r' || value == '\n')
                {
                    return index;
                }
            }

            return text.Length;
        }
    }

    internal sealed class ValuationDateMatch
    {
        public ValuationDateMatch(
            string rawDate,
            int startIndex,
            int length,
            string label,
            string boundaryEvidence)
        {
            RawDate = rawDate ?? string.Empty;
            StartIndex = startIndex;
            Length = length;
            Label = label ?? string.Empty;
            BoundaryEvidence = boundaryEvidence ?? string.Empty;
        }

        public string RawDate { get; }

        public int StartIndex { get; }

        public int Length { get; }

        public string Label { get; }

        public string BoundaryEvidence { get; }
    }
}
