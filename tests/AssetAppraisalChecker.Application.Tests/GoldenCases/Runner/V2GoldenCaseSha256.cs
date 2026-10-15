using System;
using System.IO;
using System.Security.Cryptography;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    public static class V2GoldenCaseSha256
    {
        public static string Compute(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A file path is required.", nameof(path));
            }

            using (var stream = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        public static bool Verify(string path, string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256))
            {
                throw new ArgumentException("An expected SHA256 value is required.", nameof(expectedSha256));
            }

            return string.Equals(Compute(path), expectedSha256, StringComparison.Ordinal);
        }
    }
}
