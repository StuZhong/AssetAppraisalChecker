using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class PunctuationChecker : IMechanicalDocumentChecker
    {
        private static readonly Regex RepeatedPunctuationPattern = new Regex(
            @"(?<mark>[。，；、！？：])\k<mark>+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NumberedTitlePattern = new Regex(
            @"^(?:[一二三四五六七八九十百]+、|[（(][一二三四五六七八九十百0-9]+[）)]|\d+[、.．])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NominalTitleEndingPattern = new Regex(
            @"(?:说明|影响|事项|范围|依据|假设|限制|声明)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex IntroductionEndingPattern = new Regex(
            @"(?:如下(?:表|所示)?|以下(?:事项|内容)?|包括|结论|说明)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex TableIntroductionEndingPattern = new Regex(
            @"如下表(?:所示)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Name => nameof(PunctuationChecker);

        public IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var issues = new List<MechanicalIssue>();
            ISet<int> completeRanges = MechanicalEvidenceStrength.CompleteVisibleRanges(context);
            ISet<int> tableIntroductions;
            ISet<int> nominalTitles = ReadNominalTitles(context, out tableIntroductions);
            foreach (DocumentElement element in context.Document.Elements)
            {
                string text = element.Text ?? string.Empty;
                if (text.Length == 0)
                {
                    continue;
                }

                Match repeated = RepeatedPunctuationPattern.Match(text);
                if (repeated.Success)
                {
                    AddIssue(issues, context, element,
                        MechanicalIssueKind.RepeatedPunctuation,
                        "发现明显连续重复标点",
                        "相邻位置出现同一中文标点的连续重复。",
                        repeated.Value,
                        "字符偏移=" + repeated.Index);
                }

                CheckBrackets(issues, context, element, completeRanges);
                CheckPair(issues, context, element, '“', '”',
                    MechanicalIssueKind.UnpairedChineseQuote, "中文引号不配对", completeRanges);
                CheckPair(issues, context, element, '《', '》',
                    MechanicalIssueKind.UnpairedBookTitle, "书名号不配对", completeRanges);

                if (ShouldRequireEndingPunctuation(context, element, text, nominalTitles, tableIntroductions))
                {
                    AddIssue(issues, context, element,
                        MechanicalIssueKind.MissingEndingPunctuation,
                        "完整正文段落末尾缺少结束标点",
                        "该段属于较长正文并包含句内标点，但末尾缺少有效结束标点；引导性冒号需有引导表达及后续内容。标题、表头和短标签不适用此规则。",
                        text);
                }
            }

            return MechanicalCheckerSupport.ReadOnly(issues);
        }

        private void CheckBrackets(List<MechanicalIssue> issues, MechanicalCheckContext context,
            DocumentElement element, ISet<int> completeRanges)
        {
            string text = element.Text;
            if (text.IndexOf('(') < 0 && text.IndexOf(')') < 0)
            {
                CheckPair(issues, context, element, '（', '）',
                    MechanicalIssueKind.UnpairedChineseBracket, "中文括号不配对", completeRanges);
                return;
            }
            // Preserve the existing Chinese-bracket scope; ordinary ASCII-only expressions
            // are not newly judged. Both widths must participate when Chinese brackets occur.
            if (text.IndexOf('（') < 0 && text.IndexOf('）') < 0) return;
            var openings = new Stack<int>();
            int? unmatchedClosing = null, mixedOpening = null, mixedClosing = null;
            for (int index = 0; index < text.Length; index++)
            {
                char mark = text[index];
                if (mark == '(' || mark == '（') openings.Push(index);
                else if (mark == ')' || mark == '）')
                {
                    if (openings.Count == 0)
                    {
                        if (!unmatchedClosing.HasValue) unmatchedClosing = index;
                        continue;
                    }
                    int opening = openings.Pop();
                    if ((text[opening] == '(') != (mark == ')') && !mixedOpening.HasValue)
                    {
                        mixedOpening = opening;
                        mixedClosing = index;
                    }
                }
            }
            if (unmatchedClosing.HasValue || openings.Count > 0)
            {
                int offset = unmatchedClosing ?? openings.Last();
                AddIssue(issues, context, element, MechanicalIssueKind.UnpairedChineseBracket,
                    "中文括号不配对", "同一结构化文本元素内的成对符号数量或顺序不一致。", text,
                    "符号=（）", "字符偏移=" + offset, "长度=1",
                    completeRanges.Contains(element.DocumentOrderIndex) && !IsBalanced(
                        string.Concat(context.Document.Elements.Select(item => item.Text)).Replace('(', '（').Replace(')', '）'), '（', '）')
                        ? MechanicalEvidenceStrength.HardDeterministic : MechanicalEvidenceStrength.ContextDependent);
            }
            if (mixedOpening.HasValue)
            {
                issues.Add(new MechanicalIssue(Name, MechanicalIssueKind.BracketWidthMismatch,
                    MechanicalIssueSeverity.Normal, "标点符号", "左右括号全角/半角不一致",
                    "该处左右括号均已存在且顺序成对，但半角括号与全角括号混用，并非缺少括号。",
                    "请核对并统一这一对括号的全角/半角形式；无需补加括号，程序不会修改原文。",
                    new[] { MechanicalCheckerSupport.Occurrence(context, element, text,
                        "左括号位置（从0开始）=" + mixedOpening.Value, "右括号位置（从0开始）=" + mixedClosing.Value,
                        "字符偏移=" + mixedClosing.Value, "长度=1", MechanicalEvidenceStrength.ContextDependent) }));
            }
        }

        private void CheckPair(
            List<MechanicalIssue> issues,
            MechanicalCheckContext context,
            DocumentElement element,
            char opening,
            char closing,
            MechanicalIssueKind kind,
            string summary,
            ISet<int> completeRanges)
        {
            int balance = 0;
            bool invalid = false;
            foreach (char character in element.Text)
            {
                if (character == opening)
                {
                    balance++;
                }
                else if (character == closing)
                {
                    balance--;
                    if (balance < 0)
                    {
                        invalid = true;
                        break;
                    }
                }
            }

            if (balance != 0 || invalid)
            {
                AddIssue(issues, context, element, kind, summary,
                    "同一结构化文本元素内的成对符号数量或顺序不一致。",
                    element.Text,
                    "符号=" + opening + closing,
                    "字符偏移=" + FindUnpairedOffset(element.Text, opening, closing),
                    "长度=1",
                    completeRanges.Contains(element.DocumentOrderIndex) &&
                        !IsBalanced(string.Concat(context.Document.Elements.Select(item => item.Text)), opening, closing)
                        ? MechanicalEvidenceStrength.HardDeterministic : MechanicalEvidenceStrength.ContextDependent);
            }
        }

        private static int FindUnpairedOffset(string text, char opening, char closing)
        {
            var stack = new Stack<int>();
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == opening) stack.Push(index);
                else if (text[index] == closing)
                {
                    if (stack.Count == 0) return index;
                    stack.Pop();
                }
            }
            return stack.Count == 0 ? 0 : stack.Last();
        }

        private static bool IsBalanced(string text, char opening, char closing)
        {
            int balance = 0;
            foreach (char character in text)
            {
                if (character == opening) balance++;
                else if (character == closing && --balance < 0) return false;
            }
            return balance == 0;
        }

        private static bool ShouldRequireEndingPunctuation(
            MechanicalCheckContext context,
            DocumentElement element,
            string text,
            ISet<int> nominalTitles,
            ISet<int> tableIntroductions)
        {
            // Trim only for judgment; occurrences retain the untouched source text/offsets.
            text = text.TrimEnd();
            if (element.ElementType != DocumentElementType.Paragraph ||
                text.Length < 20 ||
                text.IndexOf('，') < 0 || nominalTitles.Contains(element.DocumentOrderIndex))
            {
                return false;
            }

            char last = text[text.Length - 1];
            if (last == '：' || last == ':')
            {
                DocumentElement following = context.Document.Elements
                    .Where(item => item.DocumentOrderIndex > element.DocumentOrderIndex &&
                        item.Location.DocumentPart == element.Location.DocumentPart && !string.IsNullOrWhiteSpace(item.Text))
                    .OrderBy(item => item.DocumentOrderIndex).FirstOrDefault();
                // A colon may introduce a table, a calculation or a numbered list without
                // a fixed cue phrase. Inspect the nearest effective structure, not all colons
                // indiscriminately and not an arbitrary later formula elsewhere in the report.
                bool existingIntroduction = following != null && (IntroductionEndingPattern.IsMatch(text.Substring(0, text.Length - 1).TrimEnd()) ||
                    following.ElementType == DocumentElementType.TableCell ||
                    (following.ElementType == DocumentElementType.Paragraph &&
                        (following.Text.IndexOf('=') >= 0 || following.Text.IndexOf('＝') >= 0 ||
                            NumberedTitlePattern.IsMatch(following.Text.TrimStart()))));
                // Extend, rather than tighten, already supported introduction relationships.
                // This additional caption bridge requires immediate OOXML siblings only.
                bool verifiedTableIntroduction = TableIntroductionEndingPattern.IsMatch(
                    text.Substring(0, text.Length - 1).TrimEnd()) &&
                    tableIntroductions.Contains(element.DocumentOrderIndex);
                return !existingIntroduction && !verifiedTableIntroduction;
            }
            return "。！？；）】》”".IndexOf(last) < 0;
        }

        private static ISet<int> ReadNominalTitles(MechanicalCheckContext context, out ISet<int> tableIntroductions)
        {
            tableIntroductions = new HashSet<int>();
            var result = new HashSet<int>();
            if (!File.Exists(context.FilePath)) return result;
            var elements = context.Document.Elements.Where(item => item.ElementType == DocumentElementType.Paragraph &&
                item.Location.DocumentPart == DocumentPart.MainBody && !item.Location.TableIndex.HasValue &&
                item.Location.ParagraphIndex.HasValue)
                .GroupBy(item => item.Location.ParagraphIndex.Value).Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single());
            using (var stream = new FileStream(context.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (WordprocessingDocument package = WordprocessingDocument.Open(stream, false))
            {
                Body body = package.MainDocumentPart?.Document?.Body;
                if (body == null) return result;
                int index = 0;
                foreach (Paragraph paragraph in body.Elements<Paragraph>())
                {
                    int current = index++;
                    if (!elements.TryGetValue(current, out DocumentElement element)) continue;
                    string text = element.Text.Trim();
                    if (string.Equals(element.Text, string.Concat(paragraph.Descendants<Text>().Select(item => item.Text)),
                        StringComparison.Ordinal) && HasAdjacentIntroducedTable(paragraph))
                        tableIntroductions.Add(element.DocumentOrderIndex);
                    // Numbering alone does not make prose a title. Also require a nominal
                    // ending, no completed sentence, and a following explanatory paragraph.
                    bool numbered = paragraph.ParagraphProperties?.NumberingProperties != null ||
                        NumberedTitlePattern.IsMatch(text);
                    string withoutNumber = NumberedTitlePattern.Replace(text, "").TrimStart();
                    if (!numbered || !NominalTitleEndingPattern.IsMatch(text) ||
                        Regex.IsMatch(text, @"[。！？；：]") ||
                        Regex.IsMatch(withoutNumber, @"^(?:本次|我们|本报告|本项目|应当|必须|不得|已经)") ||
                        !string.Equals(element.Text, string.Concat(paragraph.Descendants<Text>().Select(item => item.Text)),
                            StringComparison.Ordinal)) continue;
                    Paragraph next = paragraph.NextSibling() as Paragraph;
                    if (next == null || string.IsNullOrWhiteSpace(string.Concat(next.Descendants<Text>().Select(item => item.Text))))
                        continue;
                    result.Add(element.DocumentOrderIndex);
                }
            }
            return result;
        }

        private static bool HasAdjacentIntroducedTable(Paragraph paragraph)
        {
            var next = paragraph.NextSibling();
            var table = next as Table;
            if (table == null && next is Paragraph caption)
            {
                string title = string.Concat(caption.Descendants<Text>().Select(item => item.Text)).Trim();
                // Only one immediately adjacent short table title may bridge the relationship.
                // No blank-paragraph skipping, field reconstruction or unbounded table search.
                if (title.Length == 0 || title.Length > 60 || !title.EndsWith("表", StringComparison.Ordinal) ||
                    Regex.IsMatch(title, @"[，。！？；：:=＝]") || caption.Descendants().Any(node =>
                        node.LocalName == "fldChar" || node.LocalName == "fldSimple" || node.LocalName == "instrText" ||
                        node.LocalName == "del" || node.LocalName == "ins")) return false;
                table = caption.NextSibling() as Table;
            }
            return table != null && table.Elements<TableRow>().Any() &&
                table.Descendants<Text>().Any(item => !string.IsNullOrWhiteSpace(item.Text));
        }

        private void AddIssue(
            List<MechanicalIssue> issues,
            MechanicalCheckContext context,
            DocumentElement element,
            MechanicalIssueKind kind,
            string summary,
            string reason,
            string rawText,
            params string[] evidence)
        {
            issues.Add(new MechanicalIssue(
                Name,
                kind,
                MechanicalIssueSeverity.Normal,
                "标点符号",
                summary,
                reason,
                "请人工核对标点；程序不会修改原文。",
                new[] { MechanicalCheckerSupport.Occurrence(context, element, rawText, evidence) }));
        }
    }
}
