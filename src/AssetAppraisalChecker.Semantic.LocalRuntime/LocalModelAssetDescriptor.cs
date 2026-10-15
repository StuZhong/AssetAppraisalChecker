using System;

namespace AssetAppraisalChecker.Semantic.LocalRuntime
{
    public sealed class LocalModelAssetDescriptor
    {
        public LocalModelAssetDescriptor(
            string relativePath,
            long expectedBytes,
            string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || System.IO.Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException(
                    "模型资产必须使用非空相对路径。",
                    nameof(relativePath));
            }

            if (expectedBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedBytes));
            }

            if (!SemanticHash.IsSha256(expectedSha256))
            {
                throw new ArgumentException(
                    "模型资产 Hash 必须为 SHA-256。",
                    nameof(expectedSha256));
            }

            RelativePath = relativePath;
            ExpectedBytes = expectedBytes;
            ExpectedSha256 = expectedSha256.ToLowerInvariant();
        }

        public string RelativePath { get; }

        public long ExpectedBytes { get; }

        public string ExpectedSha256 { get; }
    }
}
