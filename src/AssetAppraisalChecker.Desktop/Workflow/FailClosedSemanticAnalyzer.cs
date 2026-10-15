using System;
using AssetAppraisalChecker.Semantic;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    internal sealed class FailClosedSemanticAnalyzer : ISemanticAnalyzer
    {
        public SemanticAnalysisResult Analyze(string textContent)
        {
            throw new InvalidOperationException(
                "本地语义运行时未配置，语义判断已按 Fail Closed 停止。");
        }
    }
}
