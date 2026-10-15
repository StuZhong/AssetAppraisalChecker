using System;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Application
{
    public sealed class V2DocumentAnalysisInput
    {
        public V2DocumentAnalysisInput(
            DocumentReadResult documentReadResult,
            string sourceDocument)
        {
            DocumentReadResult = documentReadResult ??
                throw new ArgumentNullException(nameof(documentReadResult));

            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException(
                    "来源文档标识不能为空。",
                    nameof(sourceDocument));
            }

            SourceDocument = sourceDocument;
        }

        public DocumentReadResult DocumentReadResult { get; }

        public string SourceDocument { get; }
    }
}
