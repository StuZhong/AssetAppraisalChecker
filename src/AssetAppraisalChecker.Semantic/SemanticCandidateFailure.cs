using System;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateFailure
    {
        private static readonly Regex DiagnosticCodePattern = new Regex(
            "\\A[a-z0-9][a-z0-9._-]{0,79}\\z",
            RegexOptions.CultureInvariant);

        public SemanticCandidateFailure(
            SemanticCandidateFailureCode failureCode,
            string diagnosticCode)
        {
            if (failureCode == SemanticCandidateFailureCode.None
                || !Enum.IsDefined(typeof(SemanticCandidateFailureCode), failureCode))
            {
                throw new ArgumentOutOfRangeException(nameof(failureCode));
            }

            if (string.IsNullOrWhiteSpace(diagnosticCode)
                || !DiagnosticCodePattern.IsMatch(diagnosticCode))
            {
                throw new ArgumentException(
                    "DiagnosticCode 必须是短小、脱敏的稳定代码。",
                    nameof(diagnosticCode));
            }

            FailureCode = failureCode;
            DiagnosticCode = diagnosticCode;
        }

        public SemanticCandidateFailureCode FailureCode { get; }

        public string DiagnosticCode { get; }

        public override string ToString()
        {
            return string.Concat(
                "FailureCode=",
                FailureCode,
                "; DiagnosticCode=",
                DiagnosticCode);
        }
    }
}
