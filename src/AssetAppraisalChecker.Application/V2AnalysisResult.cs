using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.CrossDocument;

namespace AssetAppraisalChecker.Application
{
    public sealed class V2AnalysisResult
    {
        public V2AnalysisResult(IEnumerable<ConsistencyResult> results)
            : this(results, new FactCheckExecutionEnvelope[0])
        {
        }

        public V2AnalysisResult(
            IEnumerable<ConsistencyResult> results,
            IEnumerable<FactCheckExecutionEnvelope> executions)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (executions == null)
            {
                throw new ArgumentNullException(nameof(executions));
            }

            List<ConsistencyResult> resultItems = results.ToList();
            if (resultItems.Any(result => result == null))
            {
                throw new ArgumentException("V2 分析结果集合不能包含 null。", nameof(results));
            }

            List<FactCheckExecutionEnvelope> executionItems = executions.ToList();
            if (executionItems.Any(execution => execution == null))
            {
                throw new ArgumentException(
                    "FactCheck 执行信封集合不能包含 null。",
                    nameof(executions));
            }

            Results = new ReadOnlyCollection<ConsistencyResult>(resultItems);
            Executions = new ReadOnlyCollection<FactCheckExecutionEnvelope>(
                executionItems);
            StatusSummary = SummarizeStatus(resultItems, executionItems);
            Evidence = new ReadOnlyCollection<string>(
                resultItems
                    .OrderBy(
                        result => result.Identity.CanonicalValue,
                        StringComparer.Ordinal)
                    .SelectMany(result => result.Evidence)
                    .Distinct(StringComparer.Ordinal)
                    .ToList());
        }

        public IReadOnlyList<ConsistencyResult> Results { get; }

        public IReadOnlyList<FactCheckExecutionEnvelope> Executions { get; }

        public ConsistencyStatus StatusSummary { get; }

        public IReadOnlyList<string> Evidence { get; }

        private static ConsistencyStatus SummarizeStatus(
            IReadOnlyCollection<ConsistencyResult> results,
            IReadOnlyCollection<FactCheckExecutionEnvelope> executions)
        {
            if (results.Any(result => result.Status == ConsistencyStatus.FAIL))
            {
                return ConsistencyStatus.FAIL;
            }

            if (results.Count == 0 ||
                results.Any(result => result.Status == ConsistencyStatus.WARNING))
            {
                return ConsistencyStatus.WARNING;
            }

            if (executions.Any(execution =>
                execution.ExecutionStatus == FactCheckExecutionStatus.Failed
                || execution.ExecutionStatus
                    == FactCheckExecutionStatus.CompletedWithWarning))
            {
                return ConsistencyStatus.WARNING;
            }

            return ConsistencyStatus.PASS;
        }
    }
}
