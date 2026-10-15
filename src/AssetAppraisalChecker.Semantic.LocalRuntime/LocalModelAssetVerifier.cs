using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace AssetAppraisalChecker.Semantic.LocalRuntime
{
    public sealed class LocalModelAssetVerifier
    {
        public VerifiedLocalModelAssets Verify(LocalSemanticRuntimeOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!Directory.Exists(options.LocalModelRoot))
            {
                throw new DirectoryNotFoundException(
                    "配置的 LocalModelRoot 不存在。");
            }

            string bgeModel = VerifyAsset(options.LocalModelRoot, options.BgeModel);
            string bgeTokenizer = VerifyAsset(options.LocalModelRoot, options.BgeTokenizer);
            string rerankerModel = VerifyAsset(options.LocalModelRoot, options.RerankerModel);
            string rerankerManifest = VerifyAsset(
                options.LocalModelRoot,
                options.RerankerManifest);
            string rerankerTokenizer = VerifyAsset(
                options.LocalModelRoot,
                options.RerankerTokenizer);
            string pairTokenizer = VerifyAsset(
                options.LocalModelRoot,
                options.PairTokenizerLibrary);

            VerifyRerankerManifest(rerankerManifest, rerankerModel);
            if (!File.Exists(options.OrtExtensionsLibraryPath))
            {
                throw new FileNotFoundException(
                    "Microsoft.ML.OnnxRuntime.Extensions native library 缺失。",
                    options.OrtExtensionsLibraryPath);
            }

            return new VerifiedLocalModelAssets(
                bgeModel,
                bgeTokenizer,
                rerankerModel,
                rerankerTokenizer,
                pairTokenizer,
                options.OrtExtensionsLibraryPath);
        }

        internal static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static string VerifyAsset(
            string root,
            LocalModelAssetDescriptor descriptor)
        {
            string fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(fullRoot, descriptor.RelativePath));
            if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("模型资产路径越界。");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "必要的本地模型资产缺失。",
                    path);
            }

            var info = new FileInfo(path);
            if (info.Length != descriptor.ExpectedBytes
                || !string.Equals(
                    ComputeSha256(path),
                    descriptor.ExpectedSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "本地模型资产 size/SHA-256 校验失败：" + descriptor.RelativePath);
            }

            return path;
        }

        private static void VerifyRerankerManifest(
            string manifestPath,
            string modelPath)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            RerankerExportManifest manifest = serializer.Deserialize<RerankerExportManifest>(
                File.ReadAllText(manifestPath));
            if (manifest == null
                || !string.Equals(manifest.schemaVersion, "1.0", StringComparison.Ordinal)
                || !string.Equals(
                    manifest.frozenRevision,
                    LocalSemanticRuntimeOptions.RerankerRevision,
                    StringComparison.Ordinal)
                || manifest.model == null
                || manifest.externalData == null
                || manifest.externalData.Count == 0)
            {
                throw new InvalidDataException("Reranker export manifest 身份无效。");
            }

            if (manifest.model.bytes != new FileInfo(modelPath).Length
                || !string.Equals(
                    manifest.model.sha256,
                    ComputeSha256(modelPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Reranker model 与 export manifest 不一致。");
            }

            string directory = Path.GetDirectoryName(manifestPath)
                ?? throw new InvalidDataException("Reranker manifest 路径无父目录。");
            string fullDirectory = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (manifest.externalData
                .GroupBy(item => item.relativePath, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                throw new InvalidDataException("Reranker external-data manifest 包含重复路径。");
            }

            foreach (ManifestAsset item in manifest.externalData)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.relativePath)
                    || item.bytes <= 0 || !SemanticHash.IsSha256(item.sha256))
                {
                    throw new InvalidDataException("Reranker external-data manifest 条目无效。");
                }

                string path = Path.GetFullPath(Path.Combine(fullDirectory, item.relativePath));
                if (!path.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(path)
                    || new FileInfo(path).Length != item.bytes
                    || !string.Equals(
                        ComputeSha256(path),
                        item.sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Reranker external-data 完整性校验失败：" + item.relativePath);
                }
            }
        }

        private sealed class RerankerExportManifest
        {
            public string schemaVersion { get; set; }

            public string frozenRevision { get; set; }

            public ManifestAsset model { get; set; }

            public List<ManifestAsset> externalData { get; set; }
        }

        private sealed class ManifestAsset
        {
            public string relativePath { get; set; }

            public long bytes { get; set; }

            public string sha256 { get; set; }
        }
    }
}
