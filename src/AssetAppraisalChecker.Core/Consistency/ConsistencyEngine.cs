using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.Consistency
{
    public sealed class ConsistencyEngine
    {
        private static readonly CultureInfo AmountCulture = CultureInfo.GetCultureInfo("zh-CN");
        private readonly MoneyTolerance tolerance;

        public ConsistencyEngine()
            : this(MoneyTolerance.OneYuan)
        {
        }

        public ConsistencyEngine(MoneyTolerance tolerance)
        {
            this.tolerance = tolerance ?? throw new ArgumentNullException(nameof(tolerance));
        }

        public ConsistencyCheckResult Check(IEnumerable<MoneyOccurrence> occurrences)
        {
            if (occurrences == null)
            {
                throw new ArgumentNullException(nameof(occurrences));
            }

            List<MoneyOccurrence> source = occurrences.ToList();
            List<MoneyOccurrence> eligible = source.Where(ComparisonEligibility.IsEligible).ToList();
            List<ComparisonGroup> groups = BuildGroups(eligible);
            var issues = new List<Issue>();

            foreach (ComparisonGroup group in groups)
            {
                Issue issue = CreateIssue(group);
                if (issue != null)
                {
                    issues.Add(issue);
                }
            }

            return new ConsistencyCheckResult(groups, issues, source.Count - eligible.Count);
        }

        // Arithmetic is a same-row relationship, not a same-field occurrence comparison.
        public IReadOnlyList<TableCalculationFinding> CheckTableCalculations(DocumentReadResult document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            return ResultTableArithmetic.Evaluate(document, tolerance.AmountYuan);
        }

        private static List<ComparisonGroup> BuildGroups(IEnumerable<MoneyOccurrence> occurrences)
        {
            return occurrences
                .GroupBy(item => ComparisonKey.From(
                    item.FieldIdentity,
                    item.ComparisonSubjectKey))
                .OrderBy(group => (int)group.Key.ValueCategory)
                .ThenBy(group => (int)group.Key.TaxBasis)
                .ThenBy(group => (int)group.Key.Scope)
                .ThenBy(group => (int)group.Key.ComparisonSubjectKey.SubjectKind)
                .ThenBy(
                    group => group.Key.ComparisonSubjectKey.NormalizedSubjectId,
                    StringComparer.Ordinal)
                .Select(group => new ComparisonGroup(group.Key, OrderOccurrences(group)))
                .ToList();
        }

        private Issue CreateIssue(ComparisonGroup group)
        {
            if (group.Occurrences.Count < 2)
            {
                return null;
            }

            List<MoneyCluster> clusters = BuildClusters(group.Occurrences, tolerance.AmountYuan);
            if (clusters.Count < 2)
            {
                return null;
            }

            IReadOnlyList<MoneyOccurrence> orderedOccurrences = OrderOccurrences(group.Occurrences).ToList();
            IReadOnlyList<MoneyOccurrence> outliers = FindSuspectedOutliers(clusters);
            string fieldName = FieldDisplayNameProvider.GetDisplayName(group.ComparisonKey);
            ClassificationConfidence confidence = orderedOccurrences.Min(item =>
                MinimumConfidence(item.ClassificationConfidence, item.SubjectConfidence));
            decimal difference = orderedOccurrences.Max(item => item.NormalizedAmountYuan) -
                                 orderedOccurrences.Min(item => item.NormalizedAmountYuan);

            return new Issue(
                CreateIssueId(group.ComparisonKey),
                IssueType.MoneyConflict,
                DetermineSeverity(group.ComparisonKey, confidence),
                group.ComparisonKey,
                fieldName,
                CreateReason(
                    fieldName,
                    GetSubjectDisplayName(group.ComparisonKey.ComparisonSubjectKey),
                    orderedOccurrences,
                    clusters),
                orderedOccurrences,
                difference,
                confidence,
                string.Format(
                    "请人工核对{0}各处{1}，并确认最终正确金额。",
                    GetSubjectDisplayName(group.ComparisonKey.ComparisonSubjectKey),
                    fieldName),
                outliers);
        }

        private static List<MoneyCluster> BuildClusters(
            IEnumerable<MoneyOccurrence> occurrences,
            decimal toleranceYuan)
        {
            var clusters = new List<MoneyCluster>();
            foreach (MoneyOccurrence occurrence in OrderOccurrencesByAmount(occurrences))
            {
                MoneyCluster current = clusters.LastOrDefault();
                if (current == null || !current.CanInclude(occurrence, toleranceYuan))
                {
                    clusters.Add(new MoneyCluster(occurrence));
                }
                else
                {
                    current.Add(occurrence);
                }
            }

            return clusters;
        }

        private static IReadOnlyList<MoneyOccurrence> FindSuspectedOutliers(
            IReadOnlyCollection<MoneyCluster> clusters)
        {
            int total = clusters.Sum(cluster => cluster.Occurrences.Count);
            if (total < 3)
            {
                return new MoneyOccurrence[0];
            }

            int largestCount = clusters.Max(cluster => cluster.Occurrences.Count);
            List<MoneyCluster> largestClusters = clusters
                .Where(cluster => cluster.Occurrences.Count == largestCount)
                .ToList();

            if (largestClusters.Count != 1 || largestCount <= total / 2)
            {
                return new MoneyOccurrence[0];
            }

            MoneyCluster majority = largestClusters[0];
            return OrderOccurrences(clusters
                .Where(cluster => !ReferenceEquals(cluster, majority))
                .SelectMany(cluster => cluster.Occurrences))
                .ToList();
        }

        private static IssueSeverity DetermineSeverity(
            ComparisonKey key,
            ClassificationConfidence confidence)
        {
            if (confidence == ClassificationConfidence.Low)
            {
                return IssueSeverity.Reminder;
            }

            bool isCoreAppraisedAmount =
                key.ValueCategory == ValueCategory.AppraisedValue ||
                key.ValueCategory == ValueCategory.AppraisedOriginalValue ||
                key.ValueCategory == ValueCategory.AppraisedNetValue;

            return isCoreAppraisedAmount && confidence == ClassificationConfidence.High
                ? IssueSeverity.Serious
                : IssueSeverity.Normal;
        }

        private static string CreateIssueId(ComparisonKey key)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "MONEY-{0:D2}-{1:D2}-{2:D2}",
                (int)key.ValueCategory,
                (int)key.TaxBasis,
                (int)key.Scope) + string.Format(
                    CultureInfo.InvariantCulture,
                    "-{0:D2}-{1:X8}",
                    (int)key.ComparisonSubjectKey.SubjectKind,
                    key.ComparisonSubjectKey.GetHashCode());
        }

        private static string CreateReason(
            string fieldName,
            string subjectName,
            IEnumerable<MoneyOccurrence> occurrences,
            IEnumerable<MoneyCluster> clusters)
        {
            List<MoneyOccurrence> occurrenceList = occurrences.ToList();
            string clusterText = string.Join(
                "和",
                clusters
                    .OrderByDescending(cluster => cluster.MaximumAmountYuan)
                    .Select(FormatCluster));
            string sections = string.Join(
                "、",
                occurrenceList
                    .Select(item => item.FieldIdentity.SectionCategory)
                    .Distinct()
                    .OrderBy(item => (int)item)
                    .Select(GetSectionDisplayName));

            return string.Format(
                "{0}的{1}在{2}处出现，但标准化金额存在{3}个不同金额簇：{4}。来源区域：{5}。",
                subjectName,
                fieldName,
                occurrenceList.Count,
                clusters.Count(),
                clusterText,
                sections);
        }

        private static ClassificationConfidence MinimumConfidence(
            ClassificationConfidence first,
            ClassificationConfidence second)
        {
            return (ClassificationConfidence)Math.Min((int)first, (int)second);
        }

        private static string GetSubjectDisplayName(ComparisonSubjectKey subjectKey)
        {
            switch (subjectKey.SubjectKind)
            {
                case SubjectKind.Project:
                    return "当前项目";
                case SubjectKind.Asset:
                    return string.Format("资产“{0}”", subjectKey.NormalizedSubjectId);
                default:
                    return "未知主体";
            }
        }

        private static string FormatCluster(MoneyCluster cluster)
        {
            if (cluster.MinimumAmountYuan == cluster.MaximumAmountYuan)
            {
                return string.Format(
                    AmountCulture,
                    "{0:N2}元（{1}处）",
                    cluster.MinimumAmountYuan,
                    cluster.Occurrences.Count);
            }

            return string.Format(
                AmountCulture,
                "{0:N2}–{1:N2}元（{2}处，容差内同簇）",
                cluster.MinimumAmountYuan,
                cluster.MaximumAmountYuan,
                cluster.Occurrences.Count);
        }

        private static string GetSectionDisplayName(SectionCategory section)
        {
            switch (section)
            {
                case SectionCategory.ReportSummary:
                    return "报告摘要";
                case SectionCategory.ValuationConclusion:
                    return "评估结论";
                case SectionCategory.ResultTable:
                    return "评估结果表";
                case SectionCategory.CalculationProcess:
                    return "计算过程";
                case SectionCategory.Explanation:
                    return "说明";
                case SectionCategory.Appendix:
                    return "附件";
                default:
                    return "其他区域";
            }
        }

        private static IOrderedEnumerable<MoneyOccurrence> OrderOccurrencesByAmount(
            IEnumerable<MoneyOccurrence> occurrences)
        {
            return OrderOccurrences(occurrences)
                .OrderBy(item => item.NormalizedAmountYuan)
                .ThenBy(item => item.Location.DocumentPart)
                .ThenBy(item => item.Location.TableIndex ?? int.MaxValue)
                .ThenBy(item => item.Location.RowIndex ?? int.MaxValue)
                .ThenBy(item => item.Location.CellIndex ?? int.MaxValue)
                .ThenBy(item => item.Location.ParagraphIndex ?? int.MaxValue)
                .ThenBy(item => item.TextStartIndex)
                .ThenBy(item => item.RawText, StringComparer.Ordinal);
        }

        private static IOrderedEnumerable<MoneyOccurrence> OrderOccurrences(
            IEnumerable<MoneyOccurrence> occurrences)
        {
            return occurrences
                .OrderBy(item => item.Location.DocumentPart)
                .ThenBy(item => item.Location.ParagraphIndex ?? int.MaxValue)
                .ThenBy(item => item.Location.TableIndex ?? int.MaxValue)
                .ThenBy(item => item.Location.RowIndex ?? int.MaxValue)
                .ThenBy(item => item.Location.CellIndex ?? int.MaxValue)
                .ThenBy(item => item.TextStartIndex)
                .ThenBy(item => item.RawText, StringComparer.Ordinal)
                .ThenBy(item => item.NormalizedAmountYuan);
        }
    }
}
