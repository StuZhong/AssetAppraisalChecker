using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.ReportValidation
{
    public enum EvaluationConclusionValidityAnomaly
    {
        ReversedRange,
        ClearlyShortPeriod,
        ClearlyLongPeriod
    }

    public sealed class ValidityDateEvidence
    {
        internal ValidityDateEvidence(DocumentElement element, string rawText, int offset, DateTime date)
        {
            Element = element;
            RawText = rawText;
            Offset = offset;
            Date = date;
        }

        public DocumentElement Element { get; }
        public string RawText { get; }
        public int Offset { get; }
        public DateTime Date { get; }
    }

    public sealed class EvaluationConclusionValidityFinding
    {
        internal EvaluationConclusionValidityFinding(EvaluationConclusionValidityAnomaly anomaly,
            ValidityDateEvidence benchmark, ValidityDateEvidence start, ValidityDateEvidence end)
        {
            Anomaly = anomaly;
            Benchmark = benchmark;
            Start = start;
            End = end;
        }

        public EvaluationConclusionValidityAnomaly Anomaly { get; }
        public ValidityDateEvidence Benchmark { get; }
        public ValidityDateEvidence Start { get; }
        public ValidityDateEvidence End { get; }
    }

    public sealed class EvaluationConclusionValidityResult
    {
        internal EvaluationConclusionValidityResult(bool isEvaluable,
            IEnumerable<EvaluationConclusionValidityFinding> findings)
        {
            IsEvaluable = isEvaluable;
            Findings = new ReadOnlyCollection<EvaluationConclusionValidityFinding>(findings.ToList());
        }

        // An empty finding list is NOT a certification of compliance. Unknown dates abstain.
        public bool IsEvaluable { get; }
        public IReadOnlyList<EvaluationConclusionValidityFinding> Findings { get; }
    }
}
