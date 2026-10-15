using System;
using System.IO;

namespace AssetAppraisalChecker.Semantic.LocalRuntime
{
    public sealed class LocalSemanticRuntimeOptions
    {
        public const string BgeModelId = "BAAI/bge-small-zh-v1.5";
        public const string BgeRevision = "7999e1d3359715c523056ef9478215996d62a620";
        public const string RerankerModelId = "BAAI/bge-reranker-v2-m3";
        public const string RerankerRevision = "953dc6f6f85a1b2dbfca4c34a2796e7dde08d41e";

        public LocalSemanticRuntimeOptions(
            string localModelRoot,
            string ortExtensionsLibraryPath,
            LocalModelAssetDescriptor bgeModel,
            LocalModelAssetDescriptor bgeTokenizer,
            LocalModelAssetDescriptor rerankerModel,
            LocalModelAssetDescriptor rerankerManifest,
            LocalModelAssetDescriptor rerankerTokenizer,
            LocalModelAssetDescriptor pairTokenizerLibrary,
            bool shadowOnly)
        {
            if (string.IsNullOrWhiteSpace(localModelRoot))
            {
                throw new ArgumentException("LocalModelRoot 不能为空。", nameof(localModelRoot));
            }

            if (string.IsNullOrWhiteSpace(ortExtensionsLibraryPath))
            {
                throw new ArgumentException(
                    "ortextensions.dll 路径不能为空。",
                    nameof(ortExtensionsLibraryPath));
            }

            LocalModelRoot = Path.GetFullPath(localModelRoot);
            OrtExtensionsLibraryPath = Path.GetFullPath(ortExtensionsLibraryPath);
            BgeModel = bgeModel ?? throw new ArgumentNullException(nameof(bgeModel));
            BgeTokenizer = bgeTokenizer ?? throw new ArgumentNullException(nameof(bgeTokenizer));
            RerankerModel = rerankerModel ?? throw new ArgumentNullException(nameof(rerankerModel));
            RerankerManifest = rerankerManifest ?? throw new ArgumentNullException(nameof(rerankerManifest));
            RerankerTokenizer = rerankerTokenizer ?? throw new ArgumentNullException(nameof(rerankerTokenizer));
            PairTokenizerLibrary = pairTokenizerLibrary
                ?? throw new ArgumentNullException(nameof(pairTokenizerLibrary));
            ShadowOnly = shadowOnly;
        }

        public string LocalModelRoot { get; }

        public string OrtExtensionsLibraryPath { get; }

        public LocalModelAssetDescriptor BgeModel { get; }

        public LocalModelAssetDescriptor BgeTokenizer { get; }

        public LocalModelAssetDescriptor RerankerModel { get; }

        public LocalModelAssetDescriptor RerankerManifest { get; }

        public LocalModelAssetDescriptor RerankerTokenizer { get; }

        public LocalModelAssetDescriptor PairTokenizerLibrary { get; }

        public bool ShadowOnly { get; }

        public static LocalSemanticRuntimeOptions CreateV2Production(
            string localModelRoot,
            string ortExtensionsLibraryPath)
        {
            return new LocalSemanticRuntimeOptions(
                localModelRoot,
                ortExtensionsLibraryPath,
                new LocalModelAssetDescriptor(
                    @"bge-small-zh-v1.5\7999e1d3359715c523056ef9478215996d62a620\runtime\fp32\model.onnx",
                    94849292,
                    "56cd9ffd6be089fe2854f0c66b8a0c075b36184f225097a7b97edda9c9dbac6a"),
                new LocalModelAssetDescriptor(
                    @"tools\b2-b2\extensions-tokenizers\bge-dedicated-bert-tokenizer.onnx",
                    110177,
                    "beb8806b51aecd225016c3208e770b3bb65b9501bf6cbac220f361f277d4033e"),
                new LocalModelAssetDescriptor(
                    @"rerankers\bge-reranker-v2-m3\953dc6f6f85a1b2dbfca4c34a2796e7dde08d41e\runtime\fp32\export-1\model.onnx",
                    1533409,
                    "5eed6be53820ddb284c01c67a5c81dcdd2a8f1f6f43364637092ce6a1a35ef13"),
                new LocalModelAssetDescriptor(
                    @"rerankers\bge-reranker-v2-m3\953dc6f6f85a1b2dbfca4c34a2796e7dde08d41e\runtime\fp32\export-1\export-manifest.json",
                    31264,
                    "af775115d25e8b2166acfe378e53e0f26a8fa6ba158ca1408710f8e7672e5a50"),
                new LocalModelAssetDescriptor(
                    @"rerankers\bge-reranker-v2-m3\953dc6f6f85a1b2dbfca4c34a2796e7dde08d41e\tokenizer\tokenizer.json",
                    17098273,
                    "69564b696052886ed0ac63fa393e928384e0f8caada38c1f4864a9bfbf379c15"),
                new LocalModelAssetDescriptor(
                    @"tools\pair-tokenizer-wrapper\target\x86_64-pc-windows-gnu\release\asset_appraisal_checker_pair_tokenizer.dll",
                    3164672,
                    "c987a51ebf2122e82d1a8fe9d5f3ce4c2aae6fd8ee8de00131025288789cddd3"),
                true);
        }
    }
}
