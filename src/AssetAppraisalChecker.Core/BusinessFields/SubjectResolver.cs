using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal static class SubjectResolver
    {
        public static SubjectResolution Resolve(
            DocumentElement element,
            string localContext,
            TableBusinessContext tableContext)
        {
            SubjectResolution project;
            if (ProjectSubjectResolver.TryResolve(
                localContext,
                "当前金额所在子句",
                out project))
            {
                return project;
            }

            string matchedHeading;
            SectionCategory sectionCategory = SectionClassifier.Classify(
                element.HeadingPath,
                out matchedHeading);
            string aggregateCue;
            if (ProjectAggregateContextMatcher.TryMatch(
                localContext,
                sectionCategory,
                out aggregateCue))
            {
                return new SubjectResolution(
                    ComparisonSubjectKey.Project,
                    ClassificationConfidence.High,
                    new[]
                    {
                        string.Format(
                            "当前金额所在子句包含项目汇总证据“{0}”，且位于明确的{1}区域，识别为当前文档的项目主体。",
                            aggregateCue,
                            sectionCategory)
                    });
            }

            if (tableContext.Subject.SubjectKey.SubjectKind != SubjectKind.Unknown)
            {
                return tableContext.Subject;
            }

            SubjectResolution asset;
            if (ClauseSubjectResolver.TryResolve(localContext, out asset))
            {
                return asset;
            }

            return SubjectResolution.Unknown;
        }
    }
}
