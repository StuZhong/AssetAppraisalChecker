using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner
{
    public sealed class V2GoldenCaseOpenXmlPreflight
    {
        private static readonly string[] RequiredEntries =
        {
            "[Content_Types].xml",
            "_rels/.rels",
            "word/document.xml"
        };

        private static readonly string[] ForbiddenEntryFragments =
        {
            "vbaproject",
            "/activex/",
            "/embeddings/",
            "/externallinks/",
            "altchunk"
        };

        public IReadOnlyList<V2GoldenCaseAssertionFailure> Inspect(
            string documentId,
            string path)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ArgumentException("DocumentId is required.", nameof(documentId));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("DOCX path is required.", nameof(path));
            }

            var failures = new List<V2GoldenCaseAssertionFailure>();
            string stage = "OpenXml[" + documentId + "]";

            try
            {
                using (var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
                {
                    string[] names = archive.Entries.Select(item => item.FullName).ToArray();
                    foreach (string requiredEntry in RequiredEntries)
                    {
                        if (!names.Contains(requiredEntry, StringComparer.Ordinal))
                        {
                            failures.Add(new V2GoldenCaseAssertionFailure(
                                stage,
                                "Required Open XML part '" + requiredEntry + "' is missing."));
                        }
                    }

                    if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
                    {
                        failures.Add(new V2GoldenCaseAssertionFailure(
                            stage,
                            "The package contains duplicate part names."));
                    }

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string normalizedName = entry.FullName.ToLowerInvariant();
                        if (entry.FullName.StartsWith("/", StringComparison.Ordinal)
                            || entry.FullName.IndexOf('\\') >= 0
                            || entry.FullName.Split('/').Any(segment => segment == ".."))
                        {
                            failures.Add(new V2GoldenCaseAssertionFailure(
                                stage,
                                "Unsafe Open XML part path '" + entry.FullName + "'."));
                        }

                        if (normalizedName.EndsWith(".bin", StringComparison.Ordinal)
                            || ForbiddenEntryFragments.Any(fragment =>
                                normalizedName.IndexOf(fragment, StringComparison.Ordinal) >= 0))
                        {
                            failures.Add(new V2GoldenCaseAssertionFailure(
                                stage,
                                "Forbidden active or embedded content part '"
                                + entry.FullName + "'."));
                        }

                        if (normalizedName.EndsWith(".rels", StringComparison.Ordinal))
                        {
                            InspectRelationships(entry, stage, failures);
                        }
                    }
                }
            }
            catch (InvalidDataException exception)
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    stage,
                    "Invalid Open XML package: " + exception.Message));
            }
            catch (System.Xml.XmlException exception)
            {
                failures.Add(new V2GoldenCaseAssertionFailure(
                    stage,
                    "Invalid relationship XML: " + exception.Message));
            }

            return failures.AsReadOnly();
        }

        private static void InspectRelationships(
            ZipArchiveEntry entry,
            string stage,
            ICollection<V2GoldenCaseAssertionFailure> failures)
        {
            using (Stream stream = entry.Open())
            {
                XDocument relationships = XDocument.Load(stream, LoadOptions.None);
                bool hasExternalRelationship = relationships
                    .Descendants()
                    .Any(element => string.Equals(
                        (string)element.Attribute("TargetMode"),
                        "External",
                        StringComparison.OrdinalIgnoreCase));
                if (hasExternalRelationship)
                {
                    failures.Add(new V2GoldenCaseAssertionFailure(
                        stage,
                        "External relationship found in '" + entry.FullName + "'."));
                }
            }
        }
    }
}
