using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class DuplicateContentChecker : IMechanicalDocumentChecker
    {
        private static readonly Regex RepeatedTermPattern = new Regex(
            @"(?<term>评估|根据|采用|涉及|资产|价值|报告|本次|进行)(?:\k<term>)+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RepeatedSentencePattern = new Regex(
            @"(?<sentence>[^。！？；\r\n]{4,50}[。！？；])\k<sentence>",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly HashSet<string> ControlledRepeatedTerms = new HashSet<string>(
            new[] { "评估", "根据", "采用", "涉及", "资产", "价值", "报告", "本次", "进行" }, StringComparer.Ordinal);
        // These are controlled syntactic slots, not a general Chinese segmenter or a
        // list of exceptions from customers' documents. Unknown boundaries abstain.
        private static readonly string[] LeftContext =
            { "资产", "本次", "本项目", "本报告", "采用", "进行", "开展", "完成", "应当", "应", "需" };
        private static readonly IReadOnlyDictionary<string, string[]> RightContext =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["评估"] = new[] { "报告", "说明", "方法", "价值", "工作", "结果", "结论", "对象", "范围", "基准日", "原值", "净值", "程序", "依据", "采用", "开展" },
                ["根据"] = new[] { "相关规定", "有关规定", "现有", "上述", "规定", "资料", "开展" },
                ["采用"] = new[] { "成本法", "市场法", "收益法", "重置成本法", "评估", "相关", "上述" },
                ["涉及"] = new[] { "的", "资产", "设备", "房屋", "土地" },
                ["进行"] = new[] { "评估", "检查", "核实", "测算", "分析", "比较" }
            };
        private static readonly Regex NamedValuePattern = new Regex(
            @"(?:项目名称|资产名称|委托人|产权持有人)\s*[：:]\s*[^，。；\r\n]+|[\u4e00-\u9fff]{2,40}(?:有限责任公司|股份有限公司|有限公司)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Name => nameof(DuplicateContentChecker);

        public IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var issues = new List<MechanicalIssue>();
            foreach (DocumentElement element in context.Document.Elements)
            {
                if (string.IsNullOrWhiteSpace(element.Text))
                {
                    continue;
                }

                AddMatchIssue(
                    issues,
                    context,
                    element,
                    RepeatedSentencePattern.Match(element.Text),
                    MechanicalIssueKind.DuplicateSentence,
                    "重复短句",
                    "发现完全重复的连续短句",
                    "相邻短句内容完全相同，可能是复制或录入残留。");

                string boundaryEvidence;
                Match termMatch = FindRepeatedWord(element.Text, out boundaryEvidence);

                AddMatchIssue(
                    issues,
                    context,
                    element,
                    termMatch,
                    MechanicalIssueKind.DuplicateWord,
                    "重复词语",
                    "发现疑似连续重复词语",
                    "受控词在同一连续文本中重复，且两侧符合受控词边界或句法槽位；请核对是否为重复录入。",
                    boundaryEvidence);
            }

            AddRepeatedParagraphIssues(context, issues);
            return MechanicalCheckerSupport.ReadOnly(issues);
        }

        private void AddRepeatedParagraphIssues(
            MechanicalCheckContext context,
            List<MechanicalIssue> issues)
        {
            List<DocumentElement> paragraphs = context.Document.Elements
                .Where(MechanicalCheckerSupport.IsBodyParagraph)
                .OrderBy(item => item.DocumentOrderIndex)
                .ToList();

            DocumentElement previous = null;
            foreach (DocumentElement current in paragraphs)
            {
                string text = current.Text.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                if (previous != null &&
                    text.Length >= 20 &&
                    !LooksLikeLabelOrIdentifier(text) &&
                    string.Equals(previous.Text.Trim(), text, StringComparison.Ordinal))
                {
                    issues.Add(new MechanicalIssue(
                        Name,
                        MechanicalIssueKind.DuplicateParagraph,
                        MechanicalIssueSeverity.Normal,
                        "重复内容",
                        "发现疑似重复段落",
                        "两个相邻正文段落内容完全一致。仅检查相邻正文段落，以减少模板、页眉页脚固定内容造成的误报。",
                        "请人工确认是否为复制残留；程序不会删除或修改原文。",
                        new[]
                        {
                            MechanicalCheckerSupport.Occurrence(
                                context,
                                previous,
                                previous.Text,
                                "重复段落（前一处）"),
                            MechanicalCheckerSupport.Occurrence(
                                context,
                                current,
                                current.Text,
                                "重复段落（后一处）")
                        }));
                }

                previous = current;
            }
        }

        private static bool LooksLikeLabelOrIdentifier(string text)
        {
            return text.IndexOf('：') >= 0 ||
                   text.IndexOf(':') >= 0 ||
                   text.StartsWith("报告编号", StringComparison.Ordinal) ||
                   text.StartsWith("项目名称", StringComparison.Ordinal);
        }

        private static Match FindRepeatedWord(string text, out string evidence)
        {
            foreach (Match match in RepeatedTermPattern.Matches(text))
            {
                if (IsInsideCitation(text, match.Index) || NamedValuePattern.Matches(text).Cast<Match>()
                    .Any(name => name.Index < match.Index + match.Length && match.Index < name.Index + name.Length))
                {
                    continue;
                }

                string term = match.Groups["term"].Value;
                int end = match.Index + match.Length;
                bool leftBoundary = match.Index == 0 || IsWordDelimiter(text[match.Index - 1]);
                bool rightBoundary = end == text.Length || IsWordDelimiter(text[end]);
                bool leftSlot = LeftContext.Any(value => match.Index >= value.Length &&
                    string.CompareOrdinal(text, match.Index - value.Length, value, 0, value.Length) == 0);
                bool rightSlot = RightContext.TryGetValue(term, out string[] continuations) &&
                    continuations.Any(value => end + value.Length <= text.Length &&
                        string.CompareOrdinal(text, end, value, 0, value.Length) == 0);

                // Nouns embedded in adjacent compounds, single-character overlaps,
                // and arbitrary repeated substrings are not independently repeated words.
                if ((leftBoundary || leftSlot) && (rightBoundary || rightSlot))
                {
                    evidence = "词边界依据=受控词:" + term + ";左=" +
                        (leftBoundary ? "文本边界" : "受控句法槽位") + ";右=" +
                        (rightBoundary ? "文本边界" : "受控句法槽位");
                    return match;
                }
            }

            evidence = null;
            return Match.Empty;
        }

        private static bool IsWordDelimiter(char character)
        {
            // Do not strip/normalize input: spans continue to use original offsets.
            return char.IsWhiteSpace(character) || char.IsPunctuation(character);
        }

        private void AddMatchIssue(
            List<MechanicalIssue> issues,
            MechanicalCheckContext context,
            DocumentElement element,
            Match match,
            MechanicalIssueKind kind,
            string fieldName,
            string summary,
            string reason,
            string boundaryEvidence = null)
        {
            if (!match.Success)
            {
                return;
            }

            issues.Add(new MechanicalIssue(
                Name,
                kind,
                MechanicalIssueSeverity.Normal,
                fieldName,
                summary,
                reason,
                "请结合上下文人工核对；程序不会修改原文。",
                new[]
                {
                    MechanicalCheckerSupport.Occurrence(
                        context,
                        element,
                        match.Value,
                        "文档字符偏移=" + match.Index,
                        "长度=" + match.Length,
                        boundaryEvidence,
                        kind == MechanicalIssueKind.DuplicateWord &&
                            ControlledRepeatedTerms.Contains(match.Groups["term"].Value) &&
                            !IsInsideCitation(element.Text, match.Index) &&
                            element.Text.IndexOf('\n') < 0 && element.ElementType != DocumentElementType.Heading
                            ? MechanicalEvidenceStrength.HardDeterministic : MechanicalEvidenceStrength.ContextDependent)
            }));
        }

        private static bool IsInsideCitation(string text, int offset)
        {
            // A contiguous string inside a regulation/title can still be a valid name.
            // Do not claim a spelling decision from a word-length/backreference heuristic.
            int opening = text.LastIndexOf('《', offset);
            int closing = text.LastIndexOf('》', offset);
            return opening > closing;
        }
    }
}
