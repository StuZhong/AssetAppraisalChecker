using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal static class LocalModelAcceptanceHash
    {
        private static readonly Regex Sha256Pattern = new Regex(
            "\\A[a-f0-9]{64}\\z",
            RegexOptions.CultureInvariant);

        internal static string ComputeFileSha256(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is required.", nameof(path));
            }

            using (FileStream stream = File.OpenRead(Path.GetFullPath(path)))
            using (SHA256 algorithm = SHA256.Create())
            {
                return ToHex(algorithm.ComputeHash(stream));
            }
        }

        internal static string ComputeTextSha256(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            using (SHA256 algorithm = SHA256.Create())
            {
                return ToHex(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)));
            }
        }

        internal static bool IsSha256(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && Sha256Pattern.IsMatch(value);
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte item in bytes)
            {
                builder.Append(item.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
