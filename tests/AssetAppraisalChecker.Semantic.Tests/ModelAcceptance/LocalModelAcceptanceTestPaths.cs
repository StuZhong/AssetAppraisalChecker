using System;
using System.IO;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal static class LocalModelAcceptanceTestPaths
    {
        internal static string BgeManifest => UnderRepository(
            "tests",
            "AssetAppraisalChecker.Semantic.Tests",
            "ModelAcceptance",
            "Data",
            "Manifests",
            "bge-small-zh-v1.5.manifest.json");

        internal static string E5Manifest => UnderRepository(
            "tests",
            "AssetAppraisalChecker.Semantic.Tests",
            "ModelAcceptance",
            "Data",
            "Manifests",
            "multilingual-e5-small.manifest.json");

        internal static string AcceptanceDataset => UnderRepository(
            "tests",
            "AssetAppraisalChecker.Semantic.Tests",
            "ModelAcceptance",
            "Data",
            "Datasets",
            "local-semantic-acceptance-v1.json");

        internal static string UnderRepository(params string[] segments)
        {
            string root = FindRepositoryRoot();
            string result = root;
            foreach (string segment in segments)
            {
                result = Path.Combine(result, segment);
            }

            return result;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AssetAppraisalChecker.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the AssetAppraisalChecker repository root.");
        }
    }
}
