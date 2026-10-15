using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Application;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Desktop.Workflow.Governance;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    internal sealed class CrossDocumentIssueMapper
    {
        internal const string DocumentASource = "DocumentA";
        internal const string DocumentBSource = "DocumentB";
        internal const string DocumentALabel = "文档 A";
        internal const string DocumentBLabel = "文档 B";

        public IReadOnlyList<ReportCheckIssue> Map(
            V2AnalysisResult analysis,
            DocumentReadResult documentA,
            DocumentReadResult documentB,
            int sourceOrderStart)
        {
            if (analysis == null)
            {
                throw new ArgumentNullException(nameof(analysis));
            }

            FactSnapshot factsA = FactSnapshot.Create(documentA, DocumentASource);
            FactSnapshot factsB = FactSnapshot.Create(documentB, DocumentBSource);
            var issues = new List<ReportCheckIssue>();

            foreach (ConsistencyResult result in analysis.Results.Where(
                item => item.Status != ConsistencyStatus.PASS))
            {
                issues.Add(MapResult(
                    result,
                    factsA,
                    factsB,
                    sourceOrderStart + issues.Count));
            }

            foreach (FactCheckExecutionEnvelope execution in analysis.Executions.Where(
                item => item.ExecutionStatus == FactCheckExecutionStatus.Failed
                    && item.Results.Count == 0))
            {
                var synthetic = new ConsistencyResult(
                    execution.FactType,
                    ConsistencyStatus.WARNING,
                    string.IsNullOrWhiteSpace(execution.ErrorInformation)
                        ? "跨文档检查执行失败，未生成猜测性结论。"
                        : execution.ErrorInformation,
                    execution.ExecutionEvidence.Count == 0
                        ? new[]
                        {
                            DocumentASource + " | ExecutionFailed",
                            DocumentBSource + " | ExecutionFailed"
                        }
                        : execution.ExecutionEvidence);
                issues.Add(MapResult(
                    synthetic,
                    factsA,
                    factsB,
                    sourceOrderStart + issues.Count));
            }

            return issues;
        }

        public IReadOnlyList<ReportCheckIssue> MapProjectName(
            IReadOnlyList<BasicFieldOccurrence> documentAOccurrences,
            IReadOnlyList<BasicFieldOccurrence> documentBOccurrences,
            int sourceOrderStart)
        {
            List<BasicFieldOccurrence> projectNamesA = documentAOccurrences
                .Where(item => item.FieldType == BasicFieldType.ProjectName)
                .ToList();
            List<BasicFieldOccurrence> projectNamesB = documentBOccurrences
                .Where(item => item.FieldType == BasicFieldType.ProjectName)
                .ToList();
            var aSet = new HashSet<BasicFieldOccurrence>(projectNamesA);
            var bSet = new HashSet<BasicFieldOccurrence>(projectNamesB);
            BasicFieldConsistencyCheckResult comparison =
                new BasicFieldConsistencyEngine().Check(projectNamesA.Concat(projectNamesB));

            return comparison.Issues
                .Where(issue => issue.IssueType == IssueType.BasicFieldConflict)
                .Where(issue => issue.Occurrences.Any(aSet.Contains)
                    && issue.Occurrences.Any(bSet.Contains))
                .Select((issue, index) => MapProjectNameIssue(
                    issue,
                    aSet,
                    bSet,
                    sourceOrderStart + index))
                .ToList();
        }

        public IReadOnlyList<ReportCheckIssue> MapMoney(
            IReadOnlyList<MoneyOccurrence> documentAOccurrences,
            IReadOnlyList<MoneyOccurrence> documentBOccurrences,
            bool assessmentValueHandledByV2,
            int sourceOrderStart)
        {
            var aSet = new HashSet<MoneyOccurrence>(documentAOccurrences);
            var bSet = new HashSet<MoneyOccurrence>(documentBOccurrences);
            ConsistencyCheckResult comparison = new ConsistencyEngine().Check(
                documentAOccurrences.Concat(documentBOccurrences));

            return comparison.Issues
                .Where(issue => issue.Occurrences.Any(aSet.Contains)
                    && issue.Occurrences.Any(bSet.Contains))
                .Where(issue => !assessmentValueHandledByV2
                    || issue.Occurrences.Any(occurrence =>
                        occurrence.FieldIdentity.ValueCategory
                            != ValueCategory.AppraisedValue))
                .Select((issue, index) => MapMoneyIssue(
                    issue,
                    aSet,
                    sourceOrderStart + index))
                .ToList();
        }

        private static ReportCheckIssue MapResult(
            ConsistencyResult result,
            FactSnapshot factsA,
            FactSnapshot factsB,
            int sourceOrder)
        {
            List<BusinessFact> matchingA = factsA.Match(result).ToList();
            List<BusinessFact> matchingB = factsB.Match(result).ToList();
            var occurrences = new List<ReportCheckOccurrence>();
            AddOccurrences(occurrences, matchingA, result, DocumentALabel, DocumentASource);
            AddOccurrences(occurrences, matchingB, result, DocumentBLabel, DocumentBSource);

            if (matchingA.Count == 0)
            {
                occurrences.Add(CreateMissingOccurrence(result, DocumentALabel, DocumentASource));
            }

            if (matchingB.Count == 0)
            {
                occurrences.Add(CreateMissingOccurrence(result, DocumentBLabel, DocumentBSource));
            }

            bool failed = result.Status == ConsistencyStatus.FAIL;
            IssueGovernanceClassification governance =
                IssueGovernancePolicy.ForCrossDocument(
                    "CrossDocument." + result.FactType + "." + result.Identity.ResultRole,
                    failed);
            return new ReportCheckIssue(
                string.Concat(
                    "CROSS-",
                    result.Identity.CanonicalValue.Replace('|', '-'),
                    "-",
                    sourceOrder.ToString("D3", CultureInfo.InvariantCulture)),
                result.FactType == FactType.AssessmentValue
                    ? IssueType.MoneyConflict
                    : IssueType.BasicFieldConflict,
                failed ? IssueSeverity.Serious : IssueSeverity.Reminder,
                result.Identity.ResultRole == AppraisedEntityFact.ResultRole
                    ? "被评估单位" : GetFactDisplayName(result.FactType),
                failed ? "跨文档不一致" : "跨文档信息需人工复核",
                result.Reason,
                "请人工核对文档 A 与文档 B 的来源、口径和业务事实；系统不判定任一文档必然正确。",
                occurrences,
                sourceOrder,
                BasicFieldType.Unknown,
                null,
                null,
                governance.Category,
                governance.Authority,
                governance.GovernanceKind);
        }

        private static void AddOccurrences(
            ICollection<ReportCheckOccurrence> target,
            IEnumerable<BusinessFact> facts,
            ConsistencyResult result,
            string documentLabel,
            string sourceDocument)
        {
            foreach (BusinessFact fact in facts)
            {
                IEnumerable<string> evidence = result.Evidence
                    .Where(item => item.StartsWith(
                        sourceDocument + " |",
                        StringComparison.Ordinal));
                var objectFact = fact as AssessmentObjectFact;
                if (objectFact != null)
                    evidence = evidence.Concat(objectFact.OwnerAliasEvidence.Concat(
                        objectFact.EquitySubjectAliasEvidence).Select(definition =>
                        "明确简称定义 | " + DocumentLocationFormatter.Format(definition.Location) +
                        " | " + definition.Text));
                var entityFact = fact as AppraisedEntityFact;
                if (entityFact != null)
                    evidence = evidence.Concat(entityFact.AliasEvidence.Select(definition =>
                        "明确简称定义 | " + DocumentLocationFormatter.Format(definition.Location) +
                        " | " + definition.Text));
                target.Add(new ReportCheckOccurrence(
                    fact.OriginalText,
                    FormatFactValue(fact),
                    fact.SourceLocation,
                    DocumentLocationFormatter.Format(fact.SourceLocation),
                    DocumentLocationFormatter.FormatArea(fact.SourceLocation),
                    GetFactSubject(fact),
                    evidence,
                    new string[0],
                    string.Empty,
                    false,
                    null,
                    documentLabel));
            }
        }

        private static ReportCheckOccurrence CreateMissingOccurrence(
            ConsistencyResult result,
            string documentLabel,
            string sourceDocument)
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                null,
                null,
                null,
                null,
                null);
            return new ReportCheckOccurrence(
                "未提取到可比较事实",
                "缺失或无法唯一确定",
                location,
                "未提取到可比较位置",
                "文档",
                string.Empty,
                result.Evidence.Where(item => item.StartsWith(
                    sourceDocument + " |",
                    StringComparison.Ordinal)),
                new string[0],
                string.Empty,
                false,
                null,
                documentLabel);
        }

        private static ReportCheckIssue MapProjectNameIssue(
            BasicFieldIssue issue,
            ISet<BasicFieldOccurrence> aSet,
            ISet<BasicFieldOccurrence> bSet,
            int sourceOrder)
        {
            List<ReportCheckOccurrence> occurrences = issue.Occurrences.Select(item =>
                new ReportCheckOccurrence(
                    item.RawValue,
                    item.HasNormalizedValue
                        ? item.NormalizedValue.CanonicalValue
                        : "无法解析",
                    item.Location,
                    DocumentLocationFormatter.Format(item.Location),
                    DocumentLocationFormatter.FormatArea(item.Location),
                    string.Empty,
                    item.Evidence,
                    string.IsNullOrWhiteSpace(item.FailureReason)
                        ? new string[0]
                        : new[] { item.FailureReason },
                    string.Empty,
                    false,
                    null,
                    aSet.Contains(item) ? DocumentALabel : DocumentBLabel))
                .ToList();
            IssueGovernanceClassification governance =
                IssueGovernancePolicy.ForCrossDocument("CrossDocument.ProjectName", true);
            return new ReportCheckIssue(
                "CROSS-PROJECT-NAME-" + sourceOrder.ToString("D3", CultureInfo.InvariantCulture),
                IssueType.BasicFieldConflict,
                issue.Severity,
                issue.FieldName,
                "跨文档不一致",
                issue.Reason,
                "请人工核对文档 A 与文档 B 的项目名称；系统不判定任一文档必然正确。",
                occurrences,
                sourceOrder,
                BasicFieldType.ProjectName,
                null,
                null,
                governance.Category,
                governance.Authority,
                governance.GovernanceKind);
        }

        private static ReportCheckIssue MapMoneyIssue(
            Issue issue,
            ISet<MoneyOccurrence> aSet,
            int sourceOrder)
        {
            List<ReportCheckOccurrence> occurrences = issue.Occurrences.Select(item =>
                new ReportCheckOccurrence(
                    item.RawText,
                    item.NormalizedAmountYuan.ToString(
                        "N2",
                        CultureInfo.GetCultureInfo("zh-CN")) + " 元",
                    item.Location,
                    DocumentLocationFormatter.Format(item.Location),
                    DocumentLocationFormatter.FormatArea(item.Location),
                    string.Empty,
                    item.ClassificationEvidence.Concat(item.SubjectEvidence),
                    item.ParseWarnings,
                    string.Empty,
                    issue.SuspectedOutlierOccurrences.Any(
                        outlier => ReferenceEquals(outlier, item)),
                    null,
                    aSet.Contains(item) ? DocumentALabel : DocumentBLabel))
                .ToList();
            IssueGovernanceClassification governance =
                IssueGovernancePolicy.ForCrossDocument("CrossDocument.Money", true);
            return new ReportCheckIssue(
                "CROSS-MONEY-" + sourceOrder.ToString("D3", CultureInfo.InvariantCulture),
                IssueType.MoneyConflict,
                issue.Severity,
                issue.FieldName,
                "跨文档不一致",
                issue.Reason,
                "请人工核对文档 A 与文档 B 的金额口径；系统不判定任一文档必然正确。",
                occurrences,
                sourceOrder,
                BasicFieldType.Unknown,
                null,
                null,
                governance.Category,
                governance.Authority,
                governance.GovernanceKind);
        }

        private static string GetFactDisplayName(FactType factType)
        {
            switch (factType)
            {
                case FactType.AssessmentObject:
                    return "评估对象";
                case FactType.AssessmentValue:
                    return "评估价值";
                case FactType.BenchmarkDate:
                    return "评估基准日";
                case FactType.ReportNumber:
                    return "报告编号";
                case FactType.EvaluationMethod:
                    return "评估方法";
                default:
                    return factType.ToString();
            }
        }

        private static string FormatFactValue(BusinessFact fact)
        {
            var value = fact as AssessmentValueFact;
            if (value?.NormalizedAmountYuan != null)
            {
                return value.NormalizedAmountYuan.Value.ToString("N2", CultureInfo.GetCultureInfo("zh-CN")) + " 元";
            }

            var benchmarkDate = fact as BenchmarkDateFact;
            if (benchmarkDate?.NormalizedDate != null)
            {
                return benchmarkDate.NormalizedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            var reportNumber = fact as ReportNumberFact;
            if (reportNumber != null)
            {
                return reportNumber.NormalizedNumber;
            }

            var method = fact as EvaluationMethodFact;
            if (method != null && method.IsStructured)
            {
                return string.Concat(method.Subject, "：", method.Method, "（", method.MethodRole, "）");
            }

            var assessmentObject = fact as AssessmentObjectFact;
            if (assessmentObject != null && assessmentObject.IsEquity)
                return string.Concat("主体=", assessmentObject.EquitySubject, "；权益类型=",
                    assessmentObject.EquityInterest == AssessmentEquityInterest.EntireShareholdersEquity
                        ? "股东全部权益价值" : "部分股权价值",
                    "；比例=", assessmentObject.EquityPercentage.Value.ToString("G29", CultureInfo.InvariantCulture), "%");
            if (assessmentObject != null && assessmentObject.Quantity.HasValue)
            {
                return string.Concat(
                    "资产类型=", assessmentObject.AssetType,
                    "；权利人=", assessmentObject.Owner,
                    "；经济行为=", assessmentObject.EconomicAction,
                    "；数量=", assessmentObject.Quantity.Value.ToString(CultureInfo.InvariantCulture),
                    "；范围=", assessmentObject.Scope);
            }

            return fact.Value ?? fact.OriginalText;
        }

        private static string GetFactSubject(BusinessFact fact)
        {
            var method = fact as EvaluationMethodFact;
            return method != null && method.IsStructured ? method.Subject : string.Empty;
        }

        private sealed class FactSnapshot
        {
            private FactSnapshot(IEnumerable<BusinessFact> facts)
            {
                Facts = facts.ToList();
            }

            private IReadOnlyList<BusinessFact> Facts { get; }

            public static FactSnapshot Create(DocumentReadResult document, string sourceDocument)
            {
                var facts = new List<BusinessFact>();
                TryAdd(facts, () => new AssessmentObjectFactExtractor().Extract(document, sourceDocument));
                TryAdd(facts, () => new AppraisedEntityFactExtractor().Extract(document, sourceDocument));
                TryAdd(facts, () => new AssessmentValueFactExtractor().Extract(document, sourceDocument));
                TryAdd(facts, () => new BenchmarkDateFactExtractor().Extract(document, sourceDocument));
                TryAdd(facts, () => new ReportNumberFactExtractor().Extract(document, sourceDocument));
                TryAdd(facts, () => new EvaluationMethodFactExtractor().Extract(document, sourceDocument));
                return new FactSnapshot(facts);
            }

            public IEnumerable<BusinessFact> Match(ConsistencyResult result)
            {
                IEnumerable<BusinessFact> matches = Facts.Where(
                    fact => fact.FactType == result.FactType);
                if (result.FactType == FactType.AssessmentObject)
                    matches = result.Identity.ResultRole == AppraisedEntityFact.ResultRole
                        ? matches.Where(fact => fact is AppraisedEntityFact)
                        : matches.Where(fact => fact is AssessmentObjectFact);
                if (result.FactType == FactType.EvaluationMethod)
                {
                    matches = matches.OfType<EvaluationMethodFact>().Where(
                        fact => !fact.IsStructured
                            || string.Equals(
                                fact.NormalizedSubjectKey,
                                result.Identity.SubjectKey,
                                StringComparison.Ordinal));
                    if (result.Identity.ResultRole == EvaluationMethodComparer.FinalResultRole)
                    {
                        matches = matches.OfType<EvaluationMethodFact>().Where(
                            fact => fact.MethodRole == EvaluationMethodRole.FinalAdopted);
                    }
                }

                return matches;
            }

            private static void TryAdd<TFact>(
                ICollection<BusinessFact> target,
                Func<IReadOnlyList<TFact>> factory)
                where TFact : BusinessFact
            {
                try
                {
                    foreach (TFact fact in factory())
                    {
                        target.Add(fact);
                    }
                }
                catch (Exception exception)
                {
                    // The Application execution envelope already owns extraction
                    // failures. Presentation mapping stays evidence-only.
                    Trace.TraceWarning(
                        "Cross-document presentation fact mapping failed: {0}",
                        exception.GetType().Name);
                }
            }
        }
    }
}
