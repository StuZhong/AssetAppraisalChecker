using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Semantic
{
    public static class SemanticHash
    {
        private static readonly Regex Sha256Pattern = new Regex(
            "\\A[a-fA-F0-9]{64}\\z",
            RegexOptions.CultureInvariant);

        public static string ComputeSha256(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte item in hash)
                {
                    builder.Append(item.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static bool IsSha256(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && Sha256Pattern.IsMatch(value);
        }
    }
}
