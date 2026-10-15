using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateAnalysis
    {
        public SemanticCandidateAnalysis(
            string requestId,
            SemanticCandidateAnalyzerStatus status,
            SemanticCandidateSet candidateSet,
            SemanticCandidateFailure failure)
        {
            SemanticSourceLocation.RequireKey(requestId, nameof(requestId));
            if (!Enum.IsDefined(typeof(SemanticCandidateAnalyzerStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            CandidateSet = candidateSet
                ?? throw new ArgumentNullException(nameof(candidateSet));

            if (status == SemanticCandidateAnalyzerStatus.Succeeded)
            {
                if (failure != null)
                {
                    throw new ArgumentException(
                        "Succeeded 分析不能包含 Failure。",
                        nameof(failure));
                }
            }
            else if (failure == null)
            {
                throw new ArgumentNullException(
                    nameof(failure),
                    "Abstained/Failed 分析必须包含结构化 Failure。");
            }

            if (status == SemanticCandidateAnalyzerStatus.Failed
                && failure != null
                && (failure.FailureCode == SemanticCandidateFailureCode.LowConfidence
                    || failure.FailureCode == SemanticCandidateFailureCode.MultipleCandidates
                    || failure.FailureCode == SemanticCandidateFailureCode.UnsupportedText))
            {
                throw new ArgumentException(
                    "低置信、多候选和不支持文本应使用 Abstained，而不是 Failed。",
                    nameof(failure));
            }

            if (status == SemanticCandidateAnalyzerStatus.Abstained
                && failure != null
                && failure.FailureCode != SemanticCandidateFailureCode.LowConfidence
                && failure.FailureCode != SemanticCandidateFailureCode.MultipleCandidates
                && failure.FailureCode != SemanticCandidateFailureCode.UnsupportedText)
            {
                throw new ArgumentException(
                    "技术失败必须使用 Failed；Abstained 仅用于受控弃权。",
                    nameof(failure));
            }

            RequestId = requestId;
            Status = status;
            Failure = failure;
        }

        public string RequestId { get; }

        public SemanticCandidateAnalyzerStatus Status { get; }

        public SemanticCandidateSet CandidateSet { get; }

        public SemanticCandidateFailure Failure { get; }

        public static SemanticCandidateAnalysis Succeeded(
            string requestId,
            SemanticCandidateSet candidateSet)
        {
            return new SemanticCandidateAnalysis(
                requestId,
                SemanticCandidateAnalyzerStatus.Succeeded,
                candidateSet,
                null);
        }

        public static SemanticCandidateAnalysis Abstained(
            string requestId,
            SemanticCandidateSet candidateSet,
            SemanticCandidateFailureCode failureCode,
            string diagnosticCode)
        {
            return new SemanticCandidateAnalysis(
                requestId,
                SemanticCandidateAnalyzerStatus.Abstained,
                candidateSet,
                new SemanticCandidateFailure(failureCode, diagnosticCode));
        }

        public static SemanticCandidateAnalysis Failed(
            string requestId,
            SemanticCandidateSet candidateSet,
            SemanticCandidateFailureCode failureCode,
            string diagnosticCode)
        {
            return new SemanticCandidateAnalysis(
                requestId,
                SemanticCandidateAnalyzerStatus.Failed,
                candidateSet,
                new SemanticCandidateFailure(failureCode, diagnosticCode));
        }
    }
}
