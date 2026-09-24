using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public sealed class BasicFieldConsistencyEngine
    {
        public BasicFieldConsistencyCheckResult Check(IEnumerable<BasicFieldOccurrence> occurrences)
        {
            if (occurrences == null)
            {
                throw new ArgumentNullException(nameof(occurrences));
            }

            List<BasicFieldOccurrence> source = occurrences.ToList();
            List<BasicFieldOccurrence> normalized = source
                .Where(item => item.HasNormalizedValue)
                .ToList();
            List<BasicFieldComparisonGroup> groups = normalized
                .GroupBy(item => item.FieldType)
                .OrderBy(item => (int)item.Key)
                .Select(item => new BasicFieldComparisonGroup(item.Key, item.ToList()))
                .ToList();
            var issues = new List<BasicFieldIssue>();

            int parseIssueIndex = 0;
            foreach (BasicFieldOccurrence occurrence in source.Where(item => !item.HasNormalizedValue))
            {
                parseIssueIndex++;
                issues.Add(CreateParseIssue(occurrence, parseIssueIndex));
            }

            foreach (BasicFieldComparisonGroup group in groups)
            {
                BasicFieldIssue issue = CreateConflictIssue(group);
                if (issue != null)
                {
                    issues.Add(issue);
                }
            }

            return new BasicFieldConsistencyCheckResult(
                groups,
                issues,
                source.Count - normalized.Count);
        }

        private static BasicFieldIssue CreateConflictIssue(BasicFieldComparisonGroup group)
        {
            if (group.Occurrences.Count < 2)
            {
                return null;
            }

            List<IGrouping<string, BasicFieldOccurrence>> valueGroups = group.Occurrences
                .GroupBy(item => item.NormalizedValue.CanonicalValue, StringComparer.Ordinal)
                .OrderByDescending(item => item.Count())
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .ToList();
            if (valueGroups.Count < 2)
            {
                return null;
            }

            ClassificationConfidence confidence = group.Occurrences.Min(item => item.Confidence);
            string fieldName = BasicFieldDisplayNameProvider.GetDisplayName(group.FieldType);
            return new BasicFieldIssue(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "BASIC-CONFLICT-{0:D2}",
                    (int)group.FieldType),
                IssueType.BasicFieldConflict,
                DetermineSeverity(group.FieldType, confidence),
                group.FieldType,
                fieldName,
                CreateConflictReason(fieldName, group.Occurrences.Count, valueGroups),
                group.Occurrences,
                confidence,
                string.Format("请人工核对报告各处{0}，并确认最终正确值。", fieldName),
                FindSuspectedOutliers(valueGroups, group.Occurrences.Count));
        }

        private static BasicFieldIssue CreateParseIssue(
            BasicFieldOccurrence occurrence,
            int issueIndex)
        {
            string fieldName = BasicFieldDisplayNameProvider.GetDisplayName(occurrence.FieldType);
            string reason = string.Format(
                "发现“{0}”字段，原始值为“{1}”，但{2}",
                fieldName,
                occurrence.RawValue,
                occurrence.FailureReason);
            return new BasicFieldIssue(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "BASIC-PARSE-{0:D2}-{1:D4}",
                    (int)occurrence.FieldType,
                    issueIndex),
                IssueType.BasicFieldParseFailed,
                IssueSeverity.Reminder,
                occurrence.FieldType,
                fieldName,
                reason,
                new[] { occurrence },
                ClassificationConfidence.Low,
                string.Format("请人工核对该处{0}，并填写可明确识别的有效值。", fieldName),
                null);
        }

        private static IssueSeverity DetermineSeverity(
            BasicFieldType fieldType,
            ClassificationConfidence confidence)
        {
            if (confidence == ClassificationConfidence.Low)
            {
                return IssueSeverity.Reminder;
            }

            bool seriousField =
                fieldType == BasicFieldType.ProjectName ||
                fieldType == BasicFieldType.ReportNumber ||
                fieldType == BasicFieldType.ValuationDate ||
                fieldType == BasicFieldType.ReportIssueDate;
            if (seriousField && confidence == ClassificationConfidence.High)
            {
                return IssueSeverity.Serious;
            }

            return IssueSeverity.Normal;
        }

        private static string CreateConflictReason(
            string fieldName,
            int occurrenceCount,
            IEnumerable<IGrouping<string, BasicFieldOccurrence>> valueGroups)
        {
            string valueSummary = string.Join(
                "，",
                valueGroups.Select(group => string.Format(
                    "{0}处标准化为“{1}”",
                    group.Count(),
                    group.Key)));
            return string.Format(
                "报告中发现{0}处“{1}”，其中{2}。同一字段存在多个不同标准值。",
                occurrenceCount,
                fieldName,
                valueSummary);
        }

        private static IReadOnlyList<BasicFieldOccurrence> FindSuspectedOutliers(
            IReadOnlyList<IGrouping<string, BasicFieldOccurrence>> valueGroups,
            int totalCount)
        {
            if (totalCount < 3 || valueGroups.Count == 0)
            {
                return new BasicFieldOccurrence[0];
            }

            int largestCount = valueGroups[0].Count();
            if (largestCount <= totalCount / 2 ||
                valueGroups.Count(item => item.Count() == largestCount) != 1)
            {
                return new BasicFieldOccurrence[0];
            }

            string majorityValue = valueGroups[0].Key;
            return valueGroups
                .Where(item => !string.Equals(item.Key, majorityValue, StringComparison.Ordinal))
                .SelectMany(item => item)
                .ToList();
        }
    }
}
