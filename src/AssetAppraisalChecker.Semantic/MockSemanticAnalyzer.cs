using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class MockSemanticAnalyzer : ISemanticAnalyzer
    {
        public const string DefaultInputText = "资产评估语义分析固定测试文本";

        private readonly string _expectedInputText;
        private readonly SemanticAnalysisResult _fixedResult;

        public MockSemanticAnalyzer()
            : this(
                DefaultInputText,
                new SemanticAnalysisResult(
                    "mock-semantic-input-001",
                    "mock-semantic-representation-v1",
                    0.80m,
                    0.95m,
                    new[] { DefaultInputText }))
        {
        }

        public MockSemanticAnalyzer(
            string expectedInputText,
            SemanticAnalysisResult fixedResult)
        {
            if (string.IsNullOrWhiteSpace(expectedInputText))
            {
                throw new ArgumentException("Mock 的固定输入文本不能为空。", nameof(expectedInputText));
            }

            if (fixedResult == null)
            {
                throw new ArgumentNullException(nameof(fixedResult));
            }

            _expectedInputText = expectedInputText;
            _fixedResult = fixedResult;
        }

        public SemanticAnalysisResult Analyze(string textContent)
        {
            if (textContent == null)
            {
                throw new ArgumentNullException(nameof(textContent));
            }

            if (!string.Equals(textContent, _expectedInputText, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "MockSemanticAnalyzer 仅接受已配置的固定输入文本。",
                    nameof(textContent));
            }

            return _fixedResult;
        }
    }
}
