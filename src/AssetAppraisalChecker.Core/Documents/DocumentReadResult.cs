using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Core.Documents
{
    public sealed class DocumentReadResult
    {
        private DocumentReadResult(
            DocumentReadStatus status,
            string message,
            IEnumerable<DocumentElement> elements,
            int paragraphCount,
            int tableCount)
        {
            if (paragraphCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(paragraphCount));
            }

            if (tableCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tableCount));
            }

            Status = status;
            Message = message ?? string.Empty;
            Elements = new ReadOnlyCollection<DocumentElement>(
                (elements ?? Enumerable.Empty<DocumentElement>()).ToList());
            ParagraphCount = paragraphCount;
            TableCount = tableCount;
        }

        public DocumentReadStatus Status { get; }

        public string Message { get; }

        public IReadOnlyList<DocumentElement> Elements { get; }

        public int ParagraphCount { get; }

        public int TableCount { get; }

        public bool IsSuccess => Status == DocumentReadStatus.Success;

        public static DocumentReadResult Succeeded(
            IEnumerable<DocumentElement> elements,
            int paragraphCount,
            int tableCount)
        {
            return new DocumentReadResult(
                DocumentReadStatus.Success,
                "DOCX 文档读取成功。原文件未被修改。",
                elements,
                paragraphCount,
                tableCount);
        }

        public static DocumentReadResult Failed(DocumentReadStatus status, string message)
        {
            if (status == DocumentReadStatus.Success)
            {
                throw new ArgumentException("失败结果不能使用 Success 状态。", nameof(status));
            }

            return new DocumentReadResult(
                status,
                message,
                Enumerable.Empty<DocumentElement>(),
                0,
                0);
        }
    }
}
