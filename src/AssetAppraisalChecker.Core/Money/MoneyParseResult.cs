using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AssetAppraisalChecker.Core.Money
{
    public sealed class MoneyParseResult
    {
        private static readonly IReadOnlyList<string> NoWarnings =
            new ReadOnlyCollection<string>(new string[0]);

        private MoneyParseResult(
            string rawText,
            decimal? normalizedAmountYuan,
            MoneyUnit detectedUnit,
            MoneyNumberFormat numberFormat,
            MoneyParseStatus status,
            IReadOnlyList<string> warnings,
            MoneyParseConfidence confidence,
            string failureReason)
        {
            RawText = rawText ?? string.Empty;
            NormalizedAmountYuan = normalizedAmountYuan;
            DetectedUnit = detectedUnit;
            NumberFormat = numberFormat;
            Status = status;
            Warnings = warnings ?? NoWarnings;
            Confidence = confidence;
            FailureReason = failureReason ?? string.Empty;
        }

        public string RawText { get; }

        public decimal? NormalizedAmountYuan { get; }

        public MoneyUnit DetectedUnit { get; }

        public MoneyNumberFormat NumberFormat { get; }

        public MoneyParseStatus Status { get; }

        public IReadOnlyList<string> Warnings { get; }

        public MoneyParseConfidence Confidence { get; }

        public string FailureReason { get; }

        public bool IsSuccess =>
            NormalizedAmountYuan.HasValue &&
            (Status == MoneyParseStatus.Success || Status == MoneyParseStatus.SuccessWithWarning);

        internal static MoneyParseResult Succeeded(
            string rawText,
            decimal normalizedAmountYuan,
            MoneyUnit detectedUnit,
            MoneyNumberFormat numberFormat,
            IList<string> warnings)
        {
            if (warnings == null || warnings.Count == 0)
            {
                return new MoneyParseResult(
                    rawText,
                    normalizedAmountYuan,
                    detectedUnit,
                    numberFormat,
                    MoneyParseStatus.Success,
                    NoWarnings,
                    MoneyParseConfidence.High,
                    string.Empty);
            }

            string[] warningCopy = new string[warnings.Count];
            warnings.CopyTo(warningCopy, 0);

            return new MoneyParseResult(
                rawText,
                normalizedAmountYuan,
                detectedUnit,
                numberFormat,
                MoneyParseStatus.SuccessWithWarning,
                new ReadOnlyCollection<string>(warningCopy),
                MoneyParseConfidence.Medium,
                string.Empty);
        }

        internal static MoneyParseResult Failed(
            string rawText,
            MoneyNumberFormat numberFormat,
            string failureReason)
        {
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                throw new ArgumentException("失败结果必须包含可解释的原因。", nameof(failureReason));
            }

            return new MoneyParseResult(
                rawText,
                null,
                MoneyUnit.Unknown,
                numberFormat,
                MoneyParseStatus.Failed,
                NoWarnings,
                MoneyParseConfidence.None,
                failureReason);
        }
    }
}
