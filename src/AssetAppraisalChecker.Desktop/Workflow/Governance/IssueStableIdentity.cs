using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AssetAppraisalChecker.Desktop.Workflow.Governance
{
    internal static class IssueStableIdentity
    {
        public static string Create(
            IssueCategory category,
            string governanceKind,
            IEnumerable<ReportCheckOccurrence> occurrences)
        {
            IEnumerable<string> occurrenceKeys = (occurrences ??
                    Enumerable.Empty<ReportCheckOccurrence>())
                .Select(CreateOccurrenceKey)
                .OrderBy(item => item, StringComparer.Ordinal);
            string canonical = string.Join(
                "\n",
                new[]
                {
                    "Category=" + category,
                    "Kind=" + (governanceKind ?? string.Empty)
                }.Concat(occurrenceKeys));

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                return "ISSUE-" + BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static string CreateOccurrenceKey(ReportCheckOccurrence occurrence)
        {
            string document = string.IsNullOrWhiteSpace(occurrence.DocumentLabel)
                ? "SingleDocument"
                : Normalize(occurrence.DocumentLabel);
            string evidence = string.Join(
                "|",
                occurrence.Evidence
                    .Select(Normalize)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal));
            return string.Join(
                ";",
                new[]
                {
                    "Document=" + document,
                    "Part=" + occurrence.Location.DocumentPart,
                    "Paragraph=" + Index(occurrence.Location.ParagraphIndex),
                    "Table=" + Index(occurrence.Location.TableIndex),
                    "Row=" + Index(occurrence.Location.RowIndex),
                    "Cell=" + Index(occurrence.Location.CellIndex),
                    "Raw=" + Normalize(occurrence.RawText),
                    "Normalized=" + Normalize(occurrence.NormalizedValue),
                    "Evidence=" + evidence
                });
        }

        private static string Index(int? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            bool pendingSpace = false;
            foreach (char character in value.Trim())
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }
    }
}
