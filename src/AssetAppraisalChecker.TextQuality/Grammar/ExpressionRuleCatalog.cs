using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AssetAppraisalChecker.TextQuality.Grammar
{
    public sealed class ExpressionRuleCatalog
    {
        private static readonly ExpressionRuleCatalog DefaultCatalog =
            new ExpressionRuleCatalog(new[]
            {
                Dangling("GE-DANGLE-001", "主要包括", "“主要包括”后应有具体对象或下接清单", @"主要包括\s*[：:]?\s*$"),
                Dangling("GE-DANGLE-002", "具体为", "“具体为”后应有具体内容", @"具体为\s*[：:]?\s*$"),
                Dangling("GE-DANGLE-003", "分别为", "“分别为”后应有对应内容", @"分别为\s*[：:]?\s*$"),
                Dangling("GE-DANGLE-004", "以及", "“以及”后应有并列成分", @"以及\s*$"),
                Dangling("GE-DANGLE-005", "并且", "“并且”后应有承接内容", @"并且\s*$"),
                Dangling("GE-DANGLE-006", "根据", "“根据”后应有依据对象", @"根据\s*$"),
                Dangling("GE-DANGLE-007", "由于", "“由于”后应有原因内容", @"由于\s*$"),
                Dangling("GE-DANGLE-008", "对于", "“对于”后应有对象或事项", @"对于\s*$"),
                Dangling("GE-DANGLE-009", "通过", "“通过”后应有方式或过程", @"通过\s*$"),
                Dangling("GE-DANGLE-010", "采用", "“采用”后应有方法或方式", @"采用\s*$"),
                Dangling("GE-DANGLE-011", "具体如下", "“具体如下”后应有下接内容", @"具体如下\s*[：:]?\s*$"),
                Dangling("GE-DANGLE-012", "包括", "“包括”后应有具体对象", @"(?<!主要)包括\s*$"),
                Dangling("GE-DANGLE-013", "主要考虑了", "“主要考虑了”后应有具体因素或事项", @"主要考虑了\s*$"),
                new ExpressionRule(
                    "GE-FIXED-001",
                    GrammarExpressionKind.IncompleteControlledExpression,
                    "截至评估日为",
                    "补充完整的时间或基准日表述",
                    "受控固定表达在句末中断，缺少必要的时间内容。",
                    @"截至评估日为\s*$"),
                new ExpressionRule(
                    "GE-FIXED-002",
                    GrammarExpressionKind.IncompleteControlledExpression,
                    "采用进行",
                    "使用“采用某方法”或“使用某方法进行评估”的完整结构",
                    "“采用”和“进行”发生无对象衔接，句法结构明显不完整。",
                    @"本次评估采用进行(?:市场法|收益法|成本法|重置成本法)?\s*$"),
                new ExpressionRule(
                    "GE-FIXED-003",
                    GrammarExpressionKind.IncompleteControlledExpression,
                    "依据根据",
                    "“依据……”或“根据……”二者择一",
                    "两个同义介词无连接地叠加，固定表达结构异常。",
                    @"依据根据"),
                new ExpressionRule(
                    "GE-FIXED-004",
                    GrammarExpressionKind.IncompleteControlledExpression,
                    "评估结论使用有效期",
                    "补充完整的使用有效期条件或期限",
                    "受控有效期表达在句末中断，缺少必要的条件或期限。",
                    @"评估结论使用有效期\s*$"),
                new ExpressionRule(
                    "GE-REPEAT-001",
                    GrammarExpressionKind.RepeatedSyntacticSkeleton,
                    "进行了……进行……",
                    "保留一个完整谓语结构",
                    "同一分句内出现明显重复的“进行”谓语骨架。",
                    @"对[^，。！？；]{2,36}进行了[^，。！？；]{1,18}进行(?:核实|分析|评估|确认|检查)"),
                new ExpressionRule(
                    "GE-REPEAT-002",
                    GrammarExpressionKind.RepeatedSyntacticSkeleton,
                    "采用……进行评估采用……",
                    "拆分或去除重复的“采用”谓语骨架",
                    "同一分句中“采用……进行评估”后无连接地再次出现“采用”，句法骨架明显重复。",
                    @"采用[^，。！？；]{1,18}进行评估采用"),
                Pair("GE-PAIR-001", "虽然……但是", "“但是”后应有完整转折分句", @"虽然[^。！？；]{2,80}[，,]\s*但是\s*$"),
                Pair("GE-PAIR-002", "如果……那么", "“那么”后应有完整结果分句", @"如果[^。！？；]{2,80}[，,]\s*那么\s*$"),
                Pair("GE-PAIR-003", "不仅……而且", "“而且”后应有完整递进分句", @"不仅[^。！？；]{2,80}[，,]\s*而且\s*$"),
                Pair("GE-PAIR-004", "既……又", "“又”后应有完整并列分句", @"既[^。！？；]{2,80}[，,]\s*又\s*$"),
                Pair("GE-PAIR-005", "一方面……另一方面", "“另一方面”后应有完整并列分句", @"一方面[^。！？；]{2,80}[，,]\s*另一方面\s*$")
            });

        public ExpressionRuleCatalog(IEnumerable<ExpressionRule> rules)
        {
            Rules = new ReadOnlyCollection<ExpressionRule>(
                new List<ExpressionRule>(rules));
        }

        public IReadOnlyList<ExpressionRule> Rules { get; }

        public static ExpressionRuleCatalog Default => DefaultCatalog;

        private static ExpressionRule Dangling(
            string id,
            string trigger,
            string expected,
            string pattern)
        {
            return new ExpressionRule(
                id,
                GrammarExpressionKind.DanglingSentenceEnding,
                trigger,
                expected,
                "受控承接词位于句末，未见可以可靠确认的后续内容。",
                pattern);
        }

        private static ExpressionRule Pair(
            string id,
            string trigger,
            string expected,
            string pattern)
        {
            return new ExpressionRule(
                id,
                GrammarExpressionKind.MissingCorrelativeStructure,
                trigger,
                expected,
                "关联结构的后半部分只出现连接词，缺少可以确认的分句内容。",
                pattern);
        }
    }
}
