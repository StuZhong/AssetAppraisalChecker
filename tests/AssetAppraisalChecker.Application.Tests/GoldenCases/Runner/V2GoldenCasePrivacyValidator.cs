using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    public sealed class V2GoldenCasePrivacyValidator
    {
        public const string PolicyVersion = "v2-golden-privacy/1.0";
        public const string ScopeDisclaimer =
            "This offline preflight reduces Golden asset privacy risk; it does not prove that a document contains no personal information.";

        private const long MaximumPartBytes = 4L * 1024L * 1024L;
        private const long MaximumPackageTextBytes = 16L * 1024L * 1024L;
        private const string UsccCharacters = "0123456789ABCDEFGHJKLMNPQRTUWXY";

        private static readonly int[] UsccWeights =
        {
            1, 3, 9, 27, 19, 26, 16, 17, 20, 29, 25, 13, 8, 24, 10, 30, 28
        };

        private static readonly Regex ChineseIdPattern = new Regex(
            @"(?<!\d)(\d{17}[0-9Xx]|\d{15})(?!\d)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex ChineseMobilePattern = new Regex(
            @"(?<!\d)(1[3-9](?:[\s-]?\d){9})(?!\d)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex UsccPattern = new Regex(
            @"(?<![0-9A-Z])([0-9A-HJ-NPQRTUWXY]{18})(?![0-9A-Z])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex BankCardPattern = new Regex(
            @"(?<!\d)((?:\d[\s-]?){12,18}\d)(?!\d)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BankAccountPattern = new Regex(
            @"(?:银行账号|开户账号|银行账户|账户|卡号)\s*[:：]?\s*((?:\d[\s-]?){11,31}\d)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly HashSet<string> AllowedSyntheticMetadata =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AssetAppraisalChecker",
                "AssetAppraisalChecker Test Fixture",
                "AssetAppraisalChecker Synthetic Test",
                "Synthetic"
            };

        private static readonly HashSet<string> KnownBusinessIdentifierHashes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                ComputeTextSha256("AssetAppraisalChecker-Prohibited-Business-Identifier")
            };

        private static readonly IReadOnlyDictionary<string, string> AllowedSyntheticBinaryParts =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    {
                        "docProps/thumbnail.jpeg",
                        "96367138dc44ce09bf2c8f0f8e49348a1478d2c5c0af69bbc2bbc38b63cdcead"
                    }
                });

        public V2GoldenCasePrivacyScanResult Inspect(
            string documentId,
            string docxPath,
            string inputSha256)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("DocumentId is required.", nameof(documentId));
            }

            if (string.IsNullOrWhiteSpace(docxPath))
            {
                throw new ArgumentException("DOCX path is required.", nameof(docxPath));
            }

            if (string.IsNullOrWhiteSpace(inputSha256))
            {
                throw new ArgumentException("Input SHA256 is required.", nameof(inputSha256));
            }

            var findings = new List<V2GoldenCasePrivacyFinding>();
            try
            {
                ScanValue(Path.GetFileName(docxPath), "[package-name]", findings);
                ScanValue(documentId, "[document-id]", findings);

                long scannedBytes = 0;
                using (ZipArchive archive = ZipFile.OpenRead(Path.GetFullPath(docxPath)))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        ScanValue(entry.FullName, "[part-name]", findings);
                        if (!IsXmlPart(entry.FullName))
                        {
                            if (IsAllowedSyntheticBinaryPart(entry))
                            {
                                continue;
                            }

                            findings.Add(new V2GoldenCasePrivacyFinding(
                                "PRIV-UNSCANNABLE-PART",
                                entry.FullName,
                                "<binary-or-unknown-part>",
                                "Block"));
                            continue;
                        }

                        if (entry.Length < 0 || entry.Length > MaximumPartBytes)
                        {
                            findings.Add(new V2GoldenCasePrivacyFinding(
                                "PRIV-UNSCANNABLE-PART",
                                entry.FullName,
                                "<part-size-limit>",
                                "Block"));
                            continue;
                        }

                        scannedBytes += entry.Length;
                        if (scannedBytes > MaximumPackageTextBytes)
                        {
                            findings.Add(new V2GoldenCasePrivacyFinding(
                                "PRIV-UNSCANNABLE-PART",
                                entry.FullName,
                                "<package-size-limit>",
                                "Block"));
                            break;
                        }

                        ScanXmlPart(entry, findings);
                    }
                }
            }
            catch (Exception exception)
            {
                findings.Add(new V2GoldenCasePrivacyFinding(
                    "PRIV-SCAN-FAILED",
                    "[package]",
                    "<" + exception.GetType().Name + ">",
                    "Block"));
            }

            return new V2GoldenCasePrivacyScanResult(
                documentId,
                inputSha256,
                findings);
        }

        private static void ScanXmlPart(
            ZipArchiveEntry entry,
            ICollection<V2GoldenCasePrivacyFinding> findings)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumPartBytes,
                MaxCharactersFromEntities = 0
            };

            XDocument document;
            using (Stream stream = entry.Open())
            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                document = XDocument.Load(reader, LoadOptions.None);
            }

            foreach (XAttribute attribute in document.Descendants().Attributes())
            {
                ScanValue(attribute.Value, entry.FullName, findings);
            }

            foreach (XText text in document.DescendantNodes().OfType<XText>())
            {
                ScanValue(text.Value, entry.FullName, findings);
            }

            foreach (XElement element in document.Descendants())
            {
                if (IsProtectedMetadataElement(element)
                    && !string.IsNullOrWhiteSpace(element.Value)
                    && !AllowedSyntheticMetadata.Contains(element.Value.Trim()))
                {
                    findings.Add(new V2GoldenCasePrivacyFinding(
                        "PRIV-METADATA",
                        entry.FullName,
                        MaskGeneric(element.Value),
                        "Block"));
                }
            }
        }

        private static bool IsProtectedMetadataElement(XElement element)
        {
            return string.Equals(element.Name.LocalName, "creator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.Name.LocalName, "lastModifiedBy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.Name.LocalName, "Company", StringComparison.OrdinalIgnoreCase)
                || string.Equals(element.Name.LocalName, "Manager", StringComparison.OrdinalIgnoreCase);
        }

        private static void ScanValue(
            string value,
            string partName,
            ICollection<V2GoldenCasePrivacyFinding> findings)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (Match match in ChineseIdPattern.Matches(value))
            {
                string candidate = match.Groups[1].Value.ToUpperInvariant();
                if (IsValidChineseId(candidate))
                {
                    findings.Add(new V2GoldenCasePrivacyFinding(
                        "PRIV-CN-ID",
                        partName,
                        MaskChineseId(candidate),
                        "Block"));
                }
            }

            foreach (Match match in ChineseMobilePattern.Matches(value))
            {
                string candidate = DigitsOnly(match.Groups[1].Value);
                if (candidate.Length == 11)
                {
                    findings.Add(new V2GoldenCasePrivacyFinding(
                        "PRIV-CN-MOBILE",
                        partName,
                        candidate.Substring(0, 3) + "****" + candidate.Substring(7, 4),
                        "Block"));
                }
            }

            foreach (Match match in UsccPattern.Matches(value.ToUpperInvariant()))
            {
                string candidate = match.Groups[1].Value;
                if (IsValidUscc(candidate))
                {
                    findings.Add(new V2GoldenCasePrivacyFinding(
                        "PRIV-USCC",
                        partName,
                        candidate.Substring(0, 2) + "**************" + candidate.Substring(16, 2),
                        "Block"));
                }
            }

            foreach (Match match in BankAccountPattern.Matches(value))
            {
                string candidate = DigitsOnly(match.Groups[1].Value);
                if (candidate.Length >= 12 && candidate.Length <= 32)
                {
                    findings.Add(new V2GoldenCasePrivacyFinding(
                        "PRIV-BANK-ACCOUNT",
                        partName,
                        "********" + candidate.Substring(candidate.Length - 4, 4),
                        "Block"));
                }
            }

            foreach (Match match in BankCardPattern.Matches(value))
            {
                string candidate = DigitsOnly(match.Groups[1].Value);
                if (candidate.Length >= 13
                    && candidate.Length <= 19
                    && !IsValidChineseId(candidate)
                    && IsLuhnValid(candidate))
                {
                    findings.Add(new V2GoldenCasePrivacyFinding(
                        "PRIV-BANK-CARD",
                        partName,
                        "********" + candidate.Substring(candidate.Length - 4, 4),
                        "Block"));
                }
            }

            if (KnownBusinessIdentifierHashes.Contains(ComputeTextSha256(value.Trim())))
            {
                findings.Add(new V2GoldenCasePrivacyFinding(
                    "PRIV-KNOWN-BUSINESS",
                    partName,
                    "<redacted>",
                    "Block"));
            }
        }

        private static bool IsXmlPart(string partName)
        {
            return partName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                || partName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllowedSyntheticBinaryPart(ZipArchiveEntry entry)
        {
            string expectedHash;
            if (!AllowedSyntheticBinaryParts.TryGetValue(entry.FullName, out expectedHash))
            {
                return false;
            }

            using (Stream stream = entry.Open())
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                string actualHash = string.Concat(hash.Select(item =>
                    item.ToString("x2", CultureInfo.InvariantCulture)));
                return string.Equals(expectedHash, actualHash, StringComparison.Ordinal);
            }
        }

        private static bool IsValidChineseId(string candidate)
        {
            if (candidate.Length == 15)
            {
                return candidate.All(char.IsDigit)
                    && IsValidDate("19" + candidate.Substring(6, 6), "yyyyMMdd");
            }

            if (candidate.Length != 18
                || !candidate.Take(17).All(char.IsDigit)
                || !IsValidDate(candidate.Substring(6, 8), "yyyyMMdd"))
            {
                return false;
            }

            int[] weights =
            {
                7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2
            };
            const string checkCharacters = "10X98765432";
            int total = 0;
            for (int index = 0; index < 17; index++)
            {
                total += (candidate[index] - '0') * weights[index];
            }

            return candidate[17] == checkCharacters[total % 11];
        }

        private static bool IsValidUscc(string candidate)
        {
            if (candidate.Length != 18)
            {
                return false;
            }

            int total = 0;
            for (int index = 0; index < 17; index++)
            {
                int value = UsccCharacters.IndexOf(candidate[index]);
                if (value < 0)
                {
                    return false;
                }

                total += value * UsccWeights[index];
            }

            int checkValue = (31 - (total % 31)) % 31;
            return candidate[17] == UsccCharacters[checkValue];
        }

        private static bool IsLuhnValid(string candidate)
        {
            int total = 0;
            bool doubleDigit = false;
            for (int index = candidate.Length - 1; index >= 0; index--)
            {
                int digit = candidate[index] - '0';
                if (doubleDigit)
                {
                    digit *= 2;
                    if (digit > 9)
                    {
                        digit -= 9;
                    }
                }

                total += digit;
                doubleDigit = !doubleDigit;
            }

            return total % 10 == 0;
        }

        private static bool IsValidDate(string value, string format)
        {
            DateTime parsed;
            return DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed);
        }

        private static string DigitsOnly(string value)
        {
            return new string(value.Where(char.IsDigit).ToArray());
        }

        private static string MaskChineseId(string value)
        {
            return value.Length <= 5
                ? "<redacted>"
                : value.Substring(0, 3)
                    + new string('*', value.Length - 5)
                    + value.Substring(value.Length - 2, 2);
        }

        private static string MaskGeneric(string value)
        {
            string normalized = value.Trim();
            if (normalized.Length <= 4)
            {
                return "<redacted>";
            }

            return normalized.Substring(0, 2)
                + new string('*', Math.Min(12, normalized.Length - 4))
                + normalized.Substring(normalized.Length - 2, 2);
        }

        private static string ComputeTextSha256(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                return string.Concat(hash.Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }

    public sealed class V2GoldenCasePrivacyScanResult
    {
        public V2GoldenCasePrivacyScanResult(
            string documentId,
            string inputSha256,
            IEnumerable<V2GoldenCasePrivacyFinding> findings)
        {
            DocumentId = documentId ?? string.Empty;
            InputSha256 = inputSha256 ?? string.Empty;
            PolicyVersion = V2GoldenCasePrivacyValidator.PolicyVersion;
            Findings = new ReadOnlyCollection<V2GoldenCasePrivacyFinding>(
                new List<V2GoldenCasePrivacyFinding>(
                    findings ?? Array.Empty<V2GoldenCasePrivacyFinding>()));
        }

        public string DocumentId { get; }

        public string InputSha256 { get; }

        public string PolicyVersion { get; }

        public IReadOnlyList<V2GoldenCasePrivacyFinding> Findings { get; }

        public bool IsSafe => Findings.Count == 0;
    }

    public sealed class V2GoldenCasePrivacyFinding
    {
        public V2GoldenCasePrivacyFinding(
            string ruleId,
            string partName,
            string maskedAnchor,
            string severity)
        {
            RuleId = ruleId ?? string.Empty;
            PartName = partName ?? string.Empty;
            MaskedAnchor = maskedAnchor ?? string.Empty;
            Severity = severity ?? string.Empty;
        }

        public string RuleId { get; }

        public string PartName { get; }

        public string MaskedAnchor { get; }

        public string Severity { get; }

        public override string ToString()
        {
            return RuleId + " in " + PartName + " (" + MaskedAnchor + ")";
        }
    }
}
