using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    internal enum FormattingParagraphRole
    {
        Body,
        HeadingLike,
        TableParagraph,
        Other
    }

    internal enum FormattingScriptClass
    {
        Cjk,
        LatinOrNumber,
        Mixed,
        Other
    }

    internal sealed class RepresentativeRunFormatting
    {
        public RepresentativeRunFormatting(
            RunFormattingSnapshot run,
            decimal coverage,
            bool isMixed)
        {
            Run = run;
            Coverage = coverage;
            IsMixed = isMixed;
        }

        public RunFormattingSnapshot Run { get; }
        public decimal Coverage { get; }
        public bool IsMixed { get; }
        public bool IsUsable => Run != null && !IsMixed;
    }

    internal sealed class FormattingPeerParagraph
    {
        public FormattingPeerParagraph(
            ParagraphFormattingSnapshot paragraph,
            FormattingParagraphRole role,
            FormattingScriptClass scriptClass,
            string groupKey,
            RepresentativeRunFormatting representative)
        {
            Paragraph = paragraph;
            Role = role;
            ScriptClass = scriptClass;
            GroupKey = groupKey;
            Representative = representative;
        }

        public ParagraphFormattingSnapshot Paragraph { get; }
        public FormattingParagraphRole Role { get; }
        public FormattingScriptClass ScriptClass { get; }
        public string GroupKey { get; }
        public RepresentativeRunFormatting Representative { get; }
    }

    internal sealed class FormattingPeerGroupBuilder
    {
        internal const int LocalParagraphDistance = 8;
        internal const int MaximumLocalPeers = 8;
        internal const int MinimumComparablePeers = 4;
        internal const decimal DominanceRatio = 0.80m;
        internal const decimal RepresentativeCoverage = 0.80m;

        public IReadOnlyList<FormattingPeerParagraph> Build(DocumentFormattingSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var result = new List<FormattingPeerParagraph>();
            foreach (ParagraphFormattingSnapshot paragraph in snapshot.Paragraphs)
            {
                FormattingScriptClass scriptClass = ClassifyScript(paragraph.TextPreview);
                RepresentativeRunFormatting representative = BuildRepresentative(paragraph);
                FormattingParagraphRole role = ClassifyRole(paragraph, representative);
                result.Add(new FormattingPeerParagraph(
                    paragraph,
                    role,
                    scriptClass,
                    BuildKey(paragraph, role, scriptClass),
                    representative));
            }

            return new ReadOnlyCollection<FormattingPeerParagraph>(result);
        }

        public IReadOnlyList<FormattingPeerParagraph> GetLocalPeers(
            FormattingPeerParagraph current,
            IReadOnlyList<FormattingPeerParagraph> paragraphs)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (paragraphs == null) throw new ArgumentNullException(nameof(paragraphs));

            int index = current.Paragraph.Location.ParagraphIndex;
            return new ReadOnlyCollection<FormattingPeerParagraph>(paragraphs
                .Where(item => !ReferenceEquals(item, current))
                .Where(item => string.Equals(item.GroupKey, current.GroupKey, StringComparison.Ordinal))
                .Where(item => Math.Abs(item.Paragraph.Location.ParagraphIndex - index) <= LocalParagraphDistance)
                .OrderBy(item => Math.Abs(item.Paragraph.Location.ParagraphIndex - index))
                .ThenBy(item => item.Paragraph.Location.ParagraphIndex)
                .Take(MaximumLocalPeers)
                .ToList());
        }

        private static FormattingParagraphRole ClassifyRole(
            ParagraphFormattingSnapshot paragraph,
            RepresentativeRunFormatting representative)
        {
            if (paragraph.IsInTable) return FormattingParagraphRole.TableParagraph;
            string text = (paragraph.TextPreview ?? string.Empty).Trim();
            if (text.Length == 0) return FormattingParagraphRole.Other;

            string style = paragraph.StyleId ?? string.Empty;
            if (ContainsHeadingMarker(style)) return FormattingParagraphRole.HeadingLike;
            if (paragraph.NumberingId.IsResolved && text.Length <= 60)
            {
                return FormattingParagraphRole.HeadingLike;
            }

            bool centered = paragraph.EffectiveAlignment.IsResolved &&
                string.Equals(paragraph.EffectiveAlignment.Value, "center", StringComparison.OrdinalIgnoreCase);
            bool bold = representative.IsUsable && representative.Run.Bold.IsResolved &&
                representative.Run.Bold.Value;
            if (text.Length <= 30 && !HasSentenceEnding(text) && (centered || bold))
            {
                return FormattingParagraphRole.HeadingLike;
            }

            if (text.Length < 6 && !HasSentenceEnding(text)) return FormattingParagraphRole.Other;
            return FormattingParagraphRole.Body;
        }

        private static bool ContainsHeadingMarker(string style)
        {
            return style.IndexOf("heading", StringComparison.OrdinalIgnoreCase) >= 0 ||
                style.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                style.IndexOf("标题", StringComparison.Ordinal) >= 0;
        }

        private static bool HasSentenceEnding(string text)
        {
            char last = text[text.Length - 1];
            return "。！？；.!?;".IndexOf(last) >= 0;
        }

        private static string BuildKey(
            ParagraphFormattingSnapshot paragraph,
            FormattingParagraphRole role,
            FormattingScriptClass scriptClass)
        {
            string style = string.IsNullOrWhiteSpace(paragraph.StyleId)
                ? "<no-style>"
                : paragraph.StyleId;
            string context = paragraph.IsInTable
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "table:{0}:{1}",
                    paragraph.Location.TableIndex,
                    paragraph.TableContext?.TableStyleId ?? string.Empty)
                : "body";
            return role + "|" + style + "|" + context + "|" + scriptClass;
        }

        private static RepresentativeRunFormatting BuildRepresentative(
            ParagraphFormattingSnapshot paragraph)
        {
            List<WeightedRun> runs = paragraph.Runs
                .Where(item => !item.IsHyperlink && !item.IsField)
                .Select(item => new WeightedRun(item, CountVisibleCharacters(item.Text)))
                .Where(item => item.Weight > 0)
                .ToList();
            int total = runs.Sum(item => item.Weight);
            if (total == 0)
            {
                return new RepresentativeRunFormatting(null, 0m, true);
            }

            IGrouping<string, WeightedRun> dominant = runs
                .GroupBy(item => Signature(item.Run), StringComparer.Ordinal)
                .OrderByDescending(group => group.Sum(item => item.Weight))
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .First();
            int dominantWeight = dominant.Sum(item => item.Weight);
            decimal coverage = (decimal)dominantWeight / total;
            return new RepresentativeRunFormatting(
                dominant.First().Run,
                coverage,
                coverage < RepresentativeCoverage);
        }

        private static string Signature(RunFormattingSnapshot run)
        {
            return string.Join("|", new[]
            {
                Token(run.EffectiveFontAscii),
                Token(run.EffectiveFontHighAnsi),
                Token(run.EffectiveFontEastAsia),
                Token(run.EffectiveFontComplexScript),
                Token(run.EffectiveFontSize),
                Token(run.EffectiveFontSizeComplexScript),
                Token(run.Bold),
                Token(run.BoldComplexScript),
                Token(run.Italic),
                Token(run.ItalicComplexScript),
                Token(run.Underline),
                Token(run.Color)
            });
        }

        private static string Token<T>(FormattingValue<T> value)
        {
            return value.IsResolved
                ? Convert.ToString(value.Value, CultureInfo.InvariantCulture) + "@" + value.Unit
                : "[" + value.Status + "]";
        }

        private static int CountVisibleCharacters(string text)
        {
            return (text ?? string.Empty).Count(character => !char.IsWhiteSpace(character));
        }

        private static FormattingScriptClass ClassifyScript(string text)
        {
            bool cjk = false;
            bool latinOrNumber = false;
            foreach (char character in text ?? string.Empty)
            {
                cjk |= IsCjk(character);
                latinOrNumber |= (character >= 'A' && character <= 'Z') ||
                    (character >= 'a' && character <= 'z') || char.IsDigit(character);
            }

            if (cjk && latinOrNumber) return FormattingScriptClass.Mixed;
            if (cjk) return FormattingScriptClass.Cjk;
            if (latinOrNumber) return FormattingScriptClass.LatinOrNumber;
            return FormattingScriptClass.Other;
        }

        private static bool IsCjk(char value)
        {
            return (value >= '\u3400' && value <= '\u4DBF') ||
                (value >= '\u4E00' && value <= '\u9FFF') ||
                (value >= '\uF900' && value <= '\uFAFF');
        }

        private sealed class WeightedRun
        {
            public WeightedRun(RunFormattingSnapshot run, int weight)
            {
                Run = run;
                Weight = weight;
            }

            public RunFormattingSnapshot Run { get; }
            public int Weight { get; }
        }
    }
}
