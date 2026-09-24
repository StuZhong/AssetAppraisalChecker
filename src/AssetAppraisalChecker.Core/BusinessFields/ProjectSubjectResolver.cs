using System;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal static class ProjectSubjectResolver
    {
        private static readonly string[] ReliableProjectCues =
        {
            // More specific aggregate phrases precede their general forms.
            "本次评估项目",
            "评估对象整体",
            "评估结果合计",
            "评估结果汇总",
            "全部评估对象",
            "整体评估结果",
            "项目评估总值",
            "项目评估值",
            "项目总值",
            "评估总值",
            "项目合计",
            "本次评估",
            "本项目",
            "评估项目",
            "总计"
        };

        public static bool TryResolve(string text, string sourceDescription, out SubjectResolution resolution)
        {
            foreach (string cue in ReliableProjectCues)
            {
                if (!string.IsNullOrEmpty(text) &&
                    text.IndexOf(cue, StringComparison.Ordinal) >= 0)
                {
                    resolution = new SubjectResolution(
                        ComparisonSubjectKey.Project,
                        ClassificationConfidence.High,
                        new[]
                        {
                            string.Format("{0}包含“{1}”，识别为当前文档的项目主体。", sourceDescription, cue)
                        });
                    return true;
                }
            }

            resolution = null;
            return false;
        }
    }
}
