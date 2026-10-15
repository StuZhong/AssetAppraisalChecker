using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class SequenceChecker : IMechanicalDocumentChecker
    {
        private static readonly Regex ChineseTop = new Regex(
            @"^\s*(?<number>[一二三四五六七八九十]+)、",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ChineseParenthesized = new Regex(
            @"^\s*（(?<number>[一二三四五六七八九十]+)）",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ArabicDot = new Regex(
            @"^\s*(?<number>\d{1,3})[\.．]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ArabicComma = new Regex(
            @"^\s*(?<number>\d{1,3})、",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ArabicParenthesized = new Regex(
            @"^\s*[（(](?<number>\d{1,3})[）)]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Name => nameof(SequenceChecker);

        public IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var issues = new List<MechanicalIssue>();
            List<SequenceMarker> markers = context.Document.Elements
                .Where(item => item.ElementType != DocumentElementType.TableCell)
                .Select(Parse)
                .Where(item => item != null)
                .OrderBy(item => item.Element.DocumentOrderIndex)
                .ToList();

            SequenceMarker previous = null;
            foreach (SequenceMarker current in markers)
            {
                if (previous == null || ShouldReset(previous, current))
                {
                    previous = current;
                    continue;
                }

                if (previous.Level == current.Level && previous.Family == current.Family)
                {
                    if (current.Number == previous.Number + 1 &&
                        !string.Equals(previous.Style, current.Style, StringComparison.Ordinal))
                    {
                        AddIssue(issues, context, previous, current,
                            MechanicalIssueKind.SequenceStyleMismatch,
                            "同一级编号样式突然变化",
                            "相邻连续编号使用了不同的编号标点样式。");
                    }
                    else if (current.Number == previous.Number)
                    {
                        AddIssue(issues, context, previous, current,
                            MechanicalIssueKind.SequenceDuplicate,
                            "发现序号重复",
                            "序号 " + current.Number + " 在同一局部编号序列中重复出现。");
                    }
                    else if (current.Number > previous.Number + 1)
                    {
                        AddIssue(issues, context, previous, current,
                            MechanicalIssueKind.SequenceJump,
                            "发现序号跳号",
                            "序号 " + previous.Number + " 后直接出现 " + current.Number + "。");
                    }
                    else if (current.Number < previous.Number)
                    {
                        AddIssue(issues, context, previous, current,
                            MechanicalIssueKind.SequenceReverse,
                            "发现序号明显逆序",
                            "序号从 " + previous.Number + " 逆序到 " + current.Number + "。");
                    }
                }
                previous = current;
            }

            return MechanicalCheckerSupport.ReadOnly(issues);
        }

        private static bool ShouldReset(SequenceMarker previous, SequenceMarker current)
        {
            if (current.Number == 1)
            {
                return true;
            }

            return current.Element.DocumentOrderIndex - previous.Element.DocumentOrderIndex > 3;
        }

        private static SequenceMarker Parse(DocumentElement element)
        {
            string text = element.Text ?? string.Empty;
            SequenceMarker marker = Match(element, text, ChineseParenthesized, "Chinese", "Nested", "ChineseParenthesized", true);
            if (marker != null) return marker;
            marker = Match(element, text, ChineseTop, "Chinese", "Top", "ChineseTop", true);
            if (marker != null) return marker;
            marker = Match(element, text, ArabicParenthesized, "Arabic", "Nested", "ArabicParenthesized", false);
            if (marker != null) return marker;
            marker = Match(element, text, ArabicDot, "Arabic", "Top", "ArabicDot", false);
            if (marker != null) return marker;
            return Match(element, text, ArabicComma, "Arabic", "Top", "ArabicComma", false);
        }

        private static SequenceMarker Match(
            DocumentElement element,
            string text,
            Regex pattern,
            string family,
            string level,
            string style,
            bool chinese)
        {
            Match match = pattern.Match(text);
            if (!match.Success)
            {
                return null;
            }

            int number = chinese
                ? ParseChineseNumber(match.Groups["number"].Value)
                : int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            return number <= 0 ? null : new SequenceMarker(element, number, family, level, style, match.Value.Trim());
        }

        private static int ParseChineseNumber(string value)
        {
            if (value == "十") return 10;
            int tenIndex = value.IndexOf('十');
            if (tenIndex >= 0)
            {
                int tens = tenIndex == 0 ? 1 : ChineseDigit(value[0]);
                int ones = tenIndex == value.Length - 1 ? 0 : ChineseDigit(value[value.Length - 1]);
                return (tens * 10) + ones;
            }

            return value.Length == 1 ? ChineseDigit(value[0]) : 0;
        }

        private static int ChineseDigit(char value)
        {
            return "一二三四五六七八九".IndexOf(value) + 1;
        }

        private void AddIssue(
            List<MechanicalIssue> issues,
            MechanicalCheckContext context,
            SequenceMarker previous,
            SequenceMarker current,
            MechanicalIssueKind kind,
            string summary,
            string reason)
        {
            issues.Add(new MechanicalIssue(
                Name,
                kind,
                MechanicalIssueSeverity.Normal,
                "序号",
                summary,
                reason,
                "请核对局部编号顺序和样式；程序不会重排编号。",
                new[]
                {
                    MechanicalCheckerSupport.Occurrence(context, previous.Element, previous.MarkerText,
                        "前一序号=" + previous.Number),
                    MechanicalCheckerSupport.Occurrence(context, current.Element, current.MarkerText,
                        "当前序号=" + current.Number)
                }));
        }

        private sealed class SequenceMarker
        {
            public SequenceMarker(DocumentElement element, int number, string family, string level, string style, string markerText)
            {
                Element = element;
                Number = number;
                Family = family;
                Level = level;
                Style = style;
                MarkerText = markerText;
            }

            public DocumentElement Element { get; }
            public int Number { get; }
            public string Family { get; }
            public string Level { get; }
            public string Style { get; }
            public string MarkerText { get; }
        }
    }
}
