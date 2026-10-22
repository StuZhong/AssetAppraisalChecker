using System;

namespace AssetAppraisalChecker.Document.Excel
{
    public sealed class ExcelSummaryAmount
    {
        internal ExcelSummaryAmount(ExcelSummaryField field, decimal rawAmount,
            string worksheetName, string cellAddress, ExcelSummaryRowKind rowKind)
        {
            Field = field;
            RawAmountTenThousandYuan = rawAmount;
            // The V3 report precision is two decimal places in ten-thousand yuan.
            // Preserve the cache separately; never compare rounded floating-point values.
            RoundedAmountTenThousandYuan = decimal.Round(rawAmount, 2, MidpointRounding.AwayFromZero);
            WorksheetName = worksheetName;
            CellAddress = cellAddress;
            RowKind = rowKind;
        }

        public ExcelSummaryField Field { get; }
        public decimal RawAmountTenThousandYuan { get; }
        public decimal RoundedAmountTenThousandYuan { get; }
        public string WorksheetName { get; }
        public string CellAddress { get; }
        public ExcelSummaryRowKind RowKind { get; }
    }
}
