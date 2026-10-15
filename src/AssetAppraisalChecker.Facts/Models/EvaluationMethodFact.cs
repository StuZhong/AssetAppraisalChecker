using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models
{
    public sealed class EvaluationMethodFact : BusinessFact
    {
        private static readonly Regex SubjectKeyPattern = new Regex(
            "^subject/v[1-9][0-9]*:[a-z0-9]+(?:-[a-z0-9]+)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public EvaluationMethodFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence)
            : base(FactType.EvaluationMethod, sourceDocument, sourceLocation, originalText, confidence)
        {
            Evidence = new ReadOnlyCollection<string>(new[] { originalText });
        }

        public EvaluationMethodFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string value)
            : base(
                FactType.EvaluationMethod,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                value)
        {
            Evidence = new ReadOnlyCollection<string>(new[] { originalText });
        }

        public EvaluationMethodFact(
            string method,
            EvaluationMethodCode methodCode,
            EvaluationMethodFamily methodFamily,
            EvaluationMethodSpecificity methodSpecificity,
            string subject,
            EvaluationMethodSubjectKind subjectKind,
            string normalizedSubjectKey,
            EvaluationMethodRole methodRole,
            string methodSetId,
            EvaluationMethodSetCompleteness setCompleteness,
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            IEnumerable<string> evidence)
            : base(
                FactType.EvaluationMethod,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                CreateCompatibilityValue(subject, method))
        {
            ValidateText(method, nameof(method));
            ValidateText(subject, nameof(subject));
            ValidateText(normalizedSubjectKey, nameof(normalizedSubjectKey));
            ValidateText(methodSetId, nameof(methodSetId));
            ValidateEnum(methodCode, nameof(methodCode));
            ValidateEnum(methodFamily, nameof(methodFamily));
            ValidateEnum(methodSpecificity, nameof(methodSpecificity));
            ValidateEnum(subjectKind, nameof(subjectKind));
            ValidateEnum(methodRole, nameof(methodRole));
            ValidateEnum(setCompleteness, nameof(setCompleteness));

            if (methodCode == EvaluationMethodCode.Unknown
                || methodFamily == EvaluationMethodFamily.Unknown
                || methodSpecificity == EvaluationMethodSpecificity.Unknown
                || subjectKind == EvaluationMethodSubjectKind.Unspecified)
            {
                throw new ArgumentException("结构化方法事实不能使用 Unknown/Unspecified 核心分类。", nameof(methodCode));
            }

            if (!SubjectKeyPattern.IsMatch(normalizedSubjectKey))
            {
                throw new ArgumentException("NormalizedSubjectKey 必须是版本化稳定代码。", nameof(normalizedSubjectKey));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            List<string> items = evidence.ToList();
            if (items.Count == 0 || items.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("方法事实必须包含非空 Evidence。", nameof(evidence));
            }

            Method = method;
            MethodCode = methodCode;
            MethodFamily = methodFamily;
            MethodSpecificity = methodSpecificity;
            Subject = subject;
            SubjectKind = subjectKind;
            NormalizedSubjectKey = normalizedSubjectKey;
            MethodRole = methodRole;
            MethodSetId = methodSetId;
            SetCompleteness = setCompleteness;
            Evidence = new ReadOnlyCollection<string>(items);
        }

        public string Method { get; }
        public EvaluationMethodCode MethodCode { get; }
        public EvaluationMethodFamily MethodFamily { get; }
        public EvaluationMethodSpecificity MethodSpecificity { get; }
        public string Subject { get; }
        public EvaluationMethodSubjectKind SubjectKind { get; }
        public string NormalizedSubjectKey { get; }
        public EvaluationMethodRole MethodRole { get; }
        public string MethodSetId { get; }
        public EvaluationMethodSetCompleteness SetCompleteness { get; }
        public IReadOnlyList<string> Evidence { get; }

        public bool IsStructured => MethodCode != EvaluationMethodCode.Unknown;

        private static string CreateCompatibilityValue(string subject, string method)
        {
            ValidateText(subject, nameof(subject));
            ValidateText(method, nameof(method));
            return method;
        }

        private static void ValidateText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("值不能为空。", parameterName);
            }
        }

        private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
            where TEnum : struct
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
