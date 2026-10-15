namespace AssetAppraisalChecker.Semantic.LocalRuntime
{
    public sealed class VerifiedLocalModelAssets
    {
        internal VerifiedLocalModelAssets(
            string bgeModelPath,
            string bgeTokenizerPath,
            string rerankerModelPath,
            string rerankerTokenizerPath,
            string pairTokenizerLibraryPath,
            string ortExtensionsLibraryPath)
        {
            BgeModelPath = bgeModelPath;
            BgeTokenizerPath = bgeTokenizerPath;
            RerankerModelPath = rerankerModelPath;
            RerankerTokenizerPath = rerankerTokenizerPath;
            PairTokenizerLibraryPath = pairTokenizerLibraryPath;
            OrtExtensionsLibraryPath = ortExtensionsLibraryPath;
        }

        public string BgeModelPath { get; }

        public string BgeTokenizerPath { get; }

        public string RerankerModelPath { get; }

        public string RerankerTokenizerPath { get; }

        public string PairTokenizerLibraryPath { get; }

        public string OrtExtensionsLibraryPath { get; }
    }
}
