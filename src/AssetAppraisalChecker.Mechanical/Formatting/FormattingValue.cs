using System;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    public sealed class FormattingValue<T>
    {
        private FormattingValue(
            FormattingResolutionStatus status,
            T value,
            FormattingSource source,
            string rawValue,
            string unit,
            string reference)
        {
            Status = status;
            Value = value;
            Source = source;
            RawValue = rawValue ?? string.Empty;
            Unit = unit ?? string.Empty;
            Reference = reference ?? string.Empty;
        }

        public FormattingResolutionStatus Status { get; }

        public bool IsResolved => Status == FormattingResolutionStatus.Resolved;

        public T Value { get; }

        public FormattingSource Source { get; }

        public string RawValue { get; }

        public string Unit { get; }

        public string Reference { get; }

        public static FormattingValue<T> Resolved(
            T value,
            FormattingSource source,
            string rawValue = null,
            string unit = null,
            string reference = null)
        {
            if (source == FormattingSource.Unknown)
            {
                throw new ArgumentException("已解析格式值必须具有明确来源。", nameof(source));
            }

            return new FormattingValue<T>(
                FormattingResolutionStatus.Resolved,
                value,
                source,
                rawValue,
                unit,
                reference);
        }

        public static FormattingValue<T> Unknown(string reference = null)
        {
            return new FormattingValue<T>(
                FormattingResolutionStatus.Unknown,
                default(T),
                FormattingSource.Unknown,
                null,
                null,
                reference);
        }

        public static FormattingValue<T> Unresolved(
            string reference,
            FormattingSource source = FormattingSource.Unknown)
        {
            return new FormattingValue<T>(
                FormattingResolutionStatus.Unresolved,
                default(T),
                source,
                null,
                null,
                reference);
        }

        public static FormattingValue<T> NotImplemented(string reference)
        {
            return new FormattingValue<T>(
                FormattingResolutionStatus.NotImplemented,
                default(T),
                FormattingSource.Unknown,
                null,
                null,
                reference);
        }
    }
}
