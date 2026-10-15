using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models
{
    public sealed class EvaluationMethodStatement
    {
        public EvaluationMethodStatement(
            string subject,
            EvaluationMethodSubjectKind subjectKind,
            string normalizedSubjectKey,
            EvaluationMethodCode methodCode,
            EvaluationMethodApplicability applicability,
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            IEnumerable<string> evidence)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentException("方法适用对象不能为空。", nameof(subject));
            }

            if (!Enum.IsDefined(typeof(EvaluationMethodSubjectKind), subjectKind))
            {
                throw new ArgumentOutOfRangeException(nameof(subjectKind));
            }

            if (string.IsNullOrWhiteSpace(normalizedSubjectKey))
            {
                throw new ArgumentException("标准化 SubjectKey 不能为空。", nameof(normalizedSubjectKey));
            }

            if (!Enum.IsDefined(typeof(EvaluationMethodCode), methodCode))
            {
                throw new ArgumentOutOfRangeException(nameof(methodCode));
            }

            if (!Enum.IsDefined(typeof(EvaluationMethodApplicability), applicability))
            {
                throw new ArgumentOutOfRangeException(nameof(applicability));
            }

            if (applicability == EvaluationMethodApplicability.Adopted
                && methodCode == EvaluationMethodCode.Unknown)
            {
                throw new ArgumentException("Adopted 方法声明必须具有确定的 MethodCode。", nameof(methodCode));
            }

            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException("来源文档不能为空。", nameof(sourceDocument));
            }

            SourceLocation = sourceLocation ?? throw new ArgumentNullException(nameof(sourceLocation));
            if (string.IsNullOrWhiteSpace(originalText))
            {
                throw new ArgumentException("原始文本不能为空。", nameof(originalText));
            }

            Confidence = confidence ?? throw new ArgumentNullException(nameof(confidence));
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            List<string> items = evidence.ToList();
            if (items.Count == 0 || items.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("方法声明必须包含非空 Evidence。", nameof(evidence));
            }

            Subject = subject;
            SubjectKind = subjectKind;
            NormalizedSubjectKey = normalizedSubjectKey;
            MethodCode = methodCode;
            Applicability = applicability;
            SourceDocument = sourceDocument;
            OriginalText = originalText;
            Evidence = new ReadOnlyCollection<string>(items);
        }

        public string Subject { get; }
        public EvaluationMethodSubjectKind SubjectKind { get; }
        public string NormalizedSubjectKey { get; }
        public EvaluationMethodCode MethodCode { get; }
        public EvaluationMethodApplicability Applicability { get; }
        public string SourceDocument { get; }
        public DocumentLocation SourceLocation { get; }
        public string OriginalText { get; }
        public FactConfidence Confidence { get; }
        public IReadOnlyList<string> Evidence { get; }
    }
}
