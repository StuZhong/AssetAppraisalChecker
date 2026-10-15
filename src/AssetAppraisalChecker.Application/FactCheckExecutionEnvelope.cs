using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Application
{
    public sealed class FactCheckExecutionEnvelope
    {
        private const string DefaultNotApplicableReason =
            "当前 Checker 没有适用的事实，未生成一致性结果。";

        public FactCheckExecutionEnvelope(
            string checkerName,
            FactType factType,
            FactCheckExecutionStatus executionStatus,
            ConsistencyResult consistencyResult,
            IEnumerable<string> evidence,
            string errorInformation)
            : this(
                checkerName,
                factType,
                executionStatus,
                consistencyResult == null
                    ? new ConsistencyResult[0]
                    : new[] { consistencyResult },
                new string[0],
                errorInformation,
                executionStatus == FactCheckExecutionStatus.NotApplicable
                    ? DefaultNotApplicableReason
                    : string.Empty)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            string[] suppliedEvidence = evidence.ToArray();
            if (suppliedEvidence.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "执行证据兼容投影不能包含空项。",
                    nameof(evidence));
            }

            if (!suppliedEvidence.SequenceEqual(Evidence))
            {
                throw new ArgumentException(
                    "执行证据兼容投影必须与 Result Evidence 的确定性展开一致。",
                    nameof(evidence));
            }
        }

        public FactCheckExecutionEnvelope(
            string checkerName,
            FactType factType,
            FactCheckExecutionStatus executionStatus,
            IEnumerable<ConsistencyResult> results,
            IEnumerable<string> executionEvidence,
            string errorInformation,
            string noResultReason)
        {
            if (string.IsNullOrWhiteSpace(checkerName))
            {
                throw new ArgumentException(
                    "Checker 名称不能为空。",
                    nameof(checkerName));
            }

            if (factType == FactType.Unknown
                || !Enum.IsDefined(typeof(FactType), factType))
            {
                throw new ArgumentOutOfRangeException(nameof(factType));
            }

            if (!Enum.IsDefined(typeof(FactCheckExecutionStatus), executionStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(executionStatus));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            List<ConsistencyResult> resultItems = results.ToList();
            if (resultItems.Any(result => result == null))
            {
                throw new ArgumentException(
                    "一致性结果集合不能包含 null。",
                    nameof(results));
            }

            if (resultItems.Any(result => result.FactType != factType))
            {
                throw new ArgumentException(
                    "每个一致性结果的 FactType 必须与执行信封一致。",
                    nameof(results));
            }

            if (resultItems
                .GroupBy(result => result.Identity)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "同一执行信封不能包含重复 Result Identity。",
                    nameof(results));
            }

            ValidateStatus(
                executionStatus,
                resultItems,
                errorInformation,
                noResultReason);

            if (executionEvidence == null)
            {
                throw new ArgumentNullException(nameof(executionEvidence));
            }

            List<string> executionEvidenceItems = executionEvidence.ToList();
            if (executionEvidenceItems.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "ExecutionEvidence 不能包含空项。",
                    nameof(executionEvidence));
            }

            CheckerName = checkerName;
            FactType = factType;
            ExecutionStatus = executionStatus;
            Results = new ReadOnlyCollection<ConsistencyResult>(resultItems);
            ExecutionEvidence =
                new ReadOnlyCollection<string>(executionEvidenceItems);
            Evidence = new ReadOnlyCollection<string>(
                FlattenEvidence(resultItems));
            ErrorInformation = errorInformation ?? string.Empty;
            NoResultReason = noResultReason ?? string.Empty;
        }

        public string CheckerName { get; }

        public FactType FactType { get; }

        public FactCheckExecutionStatus ExecutionStatus { get; }

        public IReadOnlyList<ConsistencyResult> Results { get; }

        public ConsistencyResult ConsistencyResult
        {
            get
            {
                if (Results.Count > 1)
                {
                    throw new InvalidOperationException(
                        "该执行信封包含多个 Result；请使用 Results 集合。");
                }

                return Results.Count == 0 ? null : Results[0];
            }
        }

        public IReadOnlyList<string> ExecutionEvidence { get; }

        public IReadOnlyList<string> Evidence { get; }

        public string ErrorInformation { get; }

        public string NoResultReason { get; }

        public static FactCheckExecutionEnvelope FromResult(
            string checkerName,
            ConsistencyResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return FromResults(
                checkerName,
                result.FactType,
                new[] { result });
        }

        public static FactCheckExecutionEnvelope FromResults(
            string checkerName,
            FactType factType,
            IEnumerable<ConsistencyResult> results,
            IEnumerable<string> executionEvidence = null)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            ConsistencyResult[] resultItems = results.ToArray();
            FactCheckExecutionStatus status = resultItems.Any(
                result => result != null
                    && result.Status == ConsistencyStatus.WARNING)
                ? FactCheckExecutionStatus.CompletedWithWarning
                : FactCheckExecutionStatus.Succeeded;

            return new FactCheckExecutionEnvelope(
                checkerName,
                factType,
                status,
                resultItems,
                executionEvidence ?? new string[0],
                null,
                null);
        }

        public static FactCheckExecutionEnvelope NotApplicable(
            string checkerName,
            FactType factType)
        {
            return NotApplicable(
                checkerName,
                factType,
                DefaultNotApplicableReason);
        }

        public static FactCheckExecutionEnvelope NotApplicable(
            string checkerName,
            FactType factType,
            string noResultReason)
        {
            return new FactCheckExecutionEnvelope(
                checkerName,
                factType,
                FactCheckExecutionStatus.NotApplicable,
                new ConsistencyResult[0],
                new string[0],
                null,
                noResultReason);
        }

        public static FactCheckExecutionEnvelope Failed(
            string checkerName,
            ConsistencyResult warningResult,
            string errorInformation)
        {
            if (warningResult == null)
            {
                throw new ArgumentNullException(nameof(warningResult));
            }

            return Failed(
                checkerName,
                warningResult.FactType,
                new[] { warningResult },
                errorInformation);
        }

        public static FactCheckExecutionEnvelope Failed(
            string checkerName,
            FactType factType,
            IEnumerable<ConsistencyResult> completedResults,
            string errorInformation,
            IEnumerable<string> executionEvidence = null)
        {
            return new FactCheckExecutionEnvelope(
                checkerName,
                factType,
                FactCheckExecutionStatus.Failed,
                completedResults ?? new ConsistencyResult[0],
                executionEvidence ?? new string[0],
                errorInformation,
                null);
        }

        private static void ValidateStatus(
            FactCheckExecutionStatus executionStatus,
            IReadOnlyCollection<ConsistencyResult> results,
            string errorInformation,
            string noResultReason)
        {
            if (executionStatus == FactCheckExecutionStatus.Failed)
            {
                if (string.IsNullOrWhiteSpace(errorInformation))
                {
                    throw new ArgumentException(
                        "Failed 执行信封必须包含错误信息。",
                        nameof(errorInformation));
                }
            }
            else if (!string.IsNullOrWhiteSpace(errorInformation))
            {
                throw new ArgumentException(
                    "只有 Failed 执行信封可以包含 ErrorInformation。",
                    nameof(errorInformation));
            }

            if (executionStatus == FactCheckExecutionStatus.NotApplicable)
            {
                if (results.Count != 0)
                {
                    throw new ArgumentException(
                        "NotApplicable 执行信封不能包含一致性结果。",
                        nameof(results));
                }

                if (string.IsNullOrWhiteSpace(noResultReason))
                {
                    throw new ArgumentException(
                        "NotApplicable 执行信封必须包含 NoResultReason。",
                        nameof(noResultReason));
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(noResultReason))
            {
                throw new ArgumentException(
                    "只有 NotApplicable 执行信封可以包含 NoResultReason。",
                    nameof(noResultReason));
            }

            if (executionStatus == FactCheckExecutionStatus.Succeeded)
            {
                if (results.Count == 0)
                {
                    throw new ArgumentException(
                        "Succeeded 执行信封必须至少包含一个一致性结果。",
                        nameof(results));
                }

                if (results.Any(result =>
                    result.Status == ConsistencyStatus.WARNING))
                {
                    throw new ArgumentException(
                        "包含 WARNING Result 的执行必须使用 CompletedWithWarning。",
                        nameof(results));
                }
            }

            if (executionStatus == FactCheckExecutionStatus.CompletedWithWarning
                && (results.Count == 0
                    || !results.Any(result =>
                        result.Status == ConsistencyStatus.WARNING)))
            {
                throw new ArgumentException(
                    "CompletedWithWarning 执行必须包含至少一个 WARNING Result。",
                    nameof(results));
            }
        }

        private static List<string> FlattenEvidence(
            IEnumerable<ConsistencyResult> results)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var flattened = new List<string>();

            foreach (ConsistencyResult result in results
                .OrderBy(
                    item => item.Identity.CanonicalValue,
                    StringComparer.Ordinal))
            {
                foreach (string item in result.Evidence)
                {
                    if (seen.Add(item))
                    {
                        flattened.Add(item);
                    }
                }
            }

            return flattened;
        }
    }
}
