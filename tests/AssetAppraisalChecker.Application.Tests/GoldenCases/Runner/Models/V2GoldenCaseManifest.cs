using System;

namespace AssetAppraisalChecker.Application.Tests.GoldenCases.Runner.Models
{
    public sealed class V2GoldenCaseManifest
    {
        public V2GoldenCaseManifest()
        {
            SchemaVersion = string.Empty;
            Cases = Array.Empty<V2GoldenCaseManifestEntry>();
        }

        public string SchemaVersion { get; set; }

        public V2GoldenCaseManifestEntry[] Cases { get; set; }
    }

    public sealed class V2GoldenCaseManifestEntry
    {
        public V2GoldenCaseManifestEntry()
        {
            CaseId = string.Empty;
            ExpectedFile = string.Empty;
            LegacyExpectedFile = string.Empty;
            LegacySchemaVersion = string.Empty;
            Inputs = Array.Empty<V2GoldenCaseInputDefinition>();
        }

        public int Order { get; set; }

        public string CaseId { get; set; }

        public string ExpectedFile { get; set; }

        public string LegacyExpectedFile { get; set; }

        public string LegacySchemaVersion { get; set; }

        public V2GoldenCaseInputDefinition[] Inputs { get; set; }
    }

    public sealed class V2GoldenCaseInputDefinition
    {
        public V2GoldenCaseInputDefinition()
        {
            DocumentId = string.Empty;
            Role = string.Empty;
            File = string.Empty;
            Sha256 = string.Empty;
        }

        public string DocumentId { get; set; }

        public string Role { get; set; }

        public string File { get; set; }

        public string Sha256 { get; set; }
    }
}
