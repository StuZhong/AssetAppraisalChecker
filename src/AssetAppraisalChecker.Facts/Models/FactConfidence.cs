using System;

namespace AssetAppraisalChecker.Facts.Models
{
    public enum FactConfidenceBand
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public sealed class FactConfidence
    {
        public FactConfidence(FactConfidenceBand band, decimal score)
        {
            if (!Enum.IsDefined(typeof(FactConfidenceBand), band))
            {
                throw new ArgumentOutOfRangeException(nameof(band));
            }

            if (score < 0m || score > 1m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(score),
                    "事实置信度分数必须在 0 到 1 之间。");
            }

            Band = band;
            Score = score;
        }

        public FactConfidenceBand Band { get; }

        public decimal Score { get; }
    }
}
