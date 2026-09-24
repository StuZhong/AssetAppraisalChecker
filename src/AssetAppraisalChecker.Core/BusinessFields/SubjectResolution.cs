using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal sealed class SubjectResolution
    {
        public static readonly SubjectResolution Unknown = new SubjectResolution(
            ComparisonSubjectKey.Unknown,
            ClassificationConfidence.Low,
            new[] { "未找到可靠的项目或资产主体，主体保持 Unknown。" });

        public SubjectResolution(
            ComparisonSubjectKey subjectKey,
            ClassificationConfidence confidence,
            IEnumerable<string> evidence)
        {
            SubjectKey = subjectKey ?? throw new ArgumentNullException(nameof(subjectKey));
            Confidence = confidence;
            Evidence = new ReadOnlyCollection<string>(
                (evidence ?? Enumerable.Empty<string>()).ToList());
        }

        public ComparisonSubjectKey SubjectKey { get; }

        public ClassificationConfidence Confidence { get; }

        public IReadOnlyList<string> Evidence { get; }
    }
}
