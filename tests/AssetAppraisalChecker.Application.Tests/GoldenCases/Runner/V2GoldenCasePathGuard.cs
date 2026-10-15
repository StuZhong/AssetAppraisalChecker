using System;
using System.IO;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    internal static class V2GoldenCasePathGuard
    {
        public static void ValidateRelativePath(string path, string name)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidDataException(name + " is required.");
            }

            if (Path.IsPathRooted(path))
            {
                throw new InvalidDataException(name + " must be relative to the Golden Case root.");
            }

            var segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(name + " cannot leave the Golden Case root.");
                }
            }
        }

        public static string ResolveExistingFile(string goldenCaseRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(goldenCaseRoot))
            {
                throw new ArgumentException("Golden Case root is required.", nameof(goldenCaseRoot));
            }

            ValidateRelativePath(relativePath, "Golden Case path");
            var root = Path.GetFullPath(goldenCaseRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Golden Case path leaves the configured root.");
            }

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Golden Case file was not found.", fullPath);
            }

            return fullPath;
        }
    }
}
