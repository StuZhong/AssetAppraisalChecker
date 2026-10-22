using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Document.Excel;
using AssetAppraisalChecker.Facts.Models.Conclusions;

namespace AssetAppraisalChecker.Application.V3
{
    public sealed class V3MoneyComparisonItem
    {
        internal V3MoneyComparisonItem(WordConclusionTarget target, WordConclusionSourceKind sourceKind,
            ValueCategory category, TaxBasis taxBasis, string wordSource, string excelSource,
            WordConclusionAmount wordAmount, ExcelSummaryAmount excelAmount, decimal? excelComparisonAmountYuan,
            decimal? differenceYuan, V3MoneyComparisonStatus status, string reason)
        {
            Target = target;
            SourceKind = sourceKind;
            ValueCategory = category;
            TaxBasis = taxBasis;
            WordSourceDocument = wordSource ?? string.Empty;
            ExcelSourceDocument = excelSource ?? string.Empty;
            WordAmount = wordAmount;
            ExcelAmount = excelAmount;
            ExcelComparisonAmountYuan = excelComparisonAmountYuan;
            DifferenceYuan = differenceYuan;
            Status = status;
            Reason = reason;
        }

        public WordConclusionTarget Target { get; }
        public WordConclusionSourceKind SourceKind { get; }
        public ValueCategory ValueCategory { get; }
        public TaxBasis TaxBasis { get; }
        public string WordSourceDocument { get; }
        public string ExcelSourceDocument { get; }
        // Keep the extraction evidence intact for a later Desktop issue mapper.
        public WordConclusionAmount WordAmount { get; }
        public ExcelSummaryAmount ExcelAmount { get; }
        public decimal? WordNormalizedAmountYuan => WordAmount?.NormalizedAmountYuan;
        public decimal? ExcelComparisonAmountYuan { get; }
        // Null means that no reliable comparison was possible; it never means zero difference.
        public decimal? DifferenceYuan { get; }
        public V3MoneyComparisonStatus Status { get; }
        public string Reason { get; }
    }
}
