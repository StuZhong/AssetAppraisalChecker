using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Facts.Models.Conclusions
{
    public sealed class WordConclusionExtractionResult
    {
        private WordConclusionExtractionResult(WordConclusionExtractionStatus status, string message,
            IEnumerable<WordConclusion> conclusions)
        {
            Status = status;
            Message = message;
            Conclusions = new ReadOnlyCollection<WordConclusion>(conclusions.ToList());
        }

        public bool IsSuccess => Status == WordConclusionExtractionStatus.Success;
        public WordConclusionExtractionStatus Status { get; }
        public string Message { get; }
        public IReadOnlyList<WordConclusion> Conclusions { get; }
        public int AmountCount => Conclusions.Sum(item => item.Amounts.Count);

        public static WordConclusionExtractionResult Succeeded(IEnumerable<WordConclusion> conclusions) =>
            new WordConclusionExtractionResult(WordConclusionExtractionStatus.Success,
                "指定评估结论金额提取完成；未执行金额一致性比较。", conclusions);

        public static WordConclusionExtractionResult Failed(WordConclusionExtractionStatus status, string reason) =>
            new WordConclusionExtractionResult(status, reason, new WordConclusion[0]);
    }
}
