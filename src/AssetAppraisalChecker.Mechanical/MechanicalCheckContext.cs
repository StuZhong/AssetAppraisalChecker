using System;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Mechanical.Formatting;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class MechanicalCheckContext
    {
        private readonly Lazy<DocumentFormattingSnapshot> _formattingSnapshot;

        public MechanicalCheckContext(
            string filePath,
            DocumentReadResult document,
            string sourceDocument)
            : this(filePath, document, sourceDocument, null)
        {
        }

        public MechanicalCheckContext(
            string filePath,
            DocumentReadResult document,
            string sourceDocument,
            DocumentFormattingSnapshot formattingSnapshot)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("文件路径不能为空。", nameof(filePath));
            }

            FilePath = filePath;
            Document = document ?? throw new ArgumentNullException(nameof(document));
            if (!document.IsSuccess)
            {
                throw new ArgumentException("机械检查只能处理已成功读取的文档。", nameof(document));
            }

            SourceDocument = string.IsNullOrWhiteSpace(sourceDocument)
                ? "文档 A"
                : sourceDocument.Trim();
            _formattingSnapshot = new Lazy<DocumentFormattingSnapshot>(
                () => formattingSnapshot ?? new DocumentFormattingSnapshotReader().Read(FilePath),
                true);
        }

        public string FilePath { get; }

        public DocumentReadResult Document { get; }

        public string SourceDocument { get; }

        public DocumentFormattingSnapshot FormattingSnapshot => _formattingSnapshot.Value;
    }
}
