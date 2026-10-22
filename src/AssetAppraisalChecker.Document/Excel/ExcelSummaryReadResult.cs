using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Document.Excel
{
    public sealed class ExcelSummaryReadResult
    {
        private ExcelSummaryReadResult(ExcelSummaryReadStatus status, string message,
            IEnumerable<ExcelSummaryAmount> amounts)
        {
            Status = status;
            Message = message;
            Amounts = new ReadOnlyCollection<ExcelSummaryAmount>(amounts.ToList());
        }

        public bool IsSuccess => Status == ExcelSummaryReadStatus.Success;
        public ExcelSummaryReadStatus Status { get; }
        public string Message { get; }
        public IReadOnlyList<ExcelSummaryAmount> Amounts { get; }

        internal static ExcelSummaryReadResult Succeeded(IEnumerable<ExcelSummaryAmount> amounts)
        {
            return new ExcelSummaryReadResult(ExcelSummaryReadStatus.Success,
                "已读取资产评估汇总表的四项资产金额和四项合计金额。原文件未被修改。", amounts);
        }

        internal static ExcelSummaryReadResult Failed(ExcelSummaryReadStatus status, string message)
        {
            // A partial set must never look like a complete or zero-valued summary.
            return new ExcelSummaryReadResult(status, message, new ExcelSummaryAmount[0]);
        }
    }
}
