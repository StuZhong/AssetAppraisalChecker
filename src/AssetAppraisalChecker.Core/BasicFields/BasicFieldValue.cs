using System;
using System.Globalization;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public sealed class BasicFieldValue
    {
        private BasicFieldValue(
            BasicFieldValueKind kind,
            string canonicalValue,
            string textValue,
            DateTime? dateValue)
        {
            if (string.IsNullOrWhiteSpace(canonicalValue))
            {
                throw new ArgumentException("基础字段标准值不能为空。", nameof(canonicalValue));
            }

            Kind = kind;
            CanonicalValue = canonicalValue;
            TextValue = textValue;
            DateValue = dateValue;
        }

        public BasicFieldValueKind Kind { get; }

        public string CanonicalValue { get; }

        public string TextValue { get; }

        public DateTime? DateValue { get; }

        public static BasicFieldValue FromText(string value)
        {
            return new BasicFieldValue(
                BasicFieldValueKind.Text,
                value,
                value,
                null);
        }

        public static BasicFieldValue FromDate(DateTime value)
        {
            DateTime date = value.Date;
            return new BasicFieldValue(
                BasicFieldValueKind.Date,
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                null,
                date);
        }
    }
}
