using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal sealed class LocalModelAcceptancePrivacyValidator
    {
        private static readonly PrivacyPattern[] Patterns =
        {
            new PrivacyPattern("ChineseResidentId", @"(?<![0-9])[0-9]{17}[0-9Xx](?![0-9])"),
            new PrivacyPattern("MobileNumber", @"(?<![0-9])1[3-9][0-9]{9}(?![0-9])"),
            new PrivacyPattern(
                "UnifiedSocialCreditCode",
                @"(?<![0-9A-Z])[0-9A-HJ-NPQRTUWXY]{18}(?![0-9A-Z])"),
            new PrivacyPattern("BankAccount", @"(?<![0-9])[0-9]{16,19}(?![0-9])")
        };

        internal LocalModelAcceptancePrivacyValidationResult Inspect(
            IEnumerable<LocalSemanticAcceptanceCase> cases)
        {
            if (cases == null)
            {
                throw new ArgumentNullException(nameof(cases));
            }

            var findings = new List<string>();
            foreach (LocalSemanticAcceptanceCase item in cases)
            {
                if (item == null)
                {
                    findings.Add("Case=<null>; Pattern=InvalidCase");
                    continue;
                }

                foreach (TextField field in EnumerateTextFields(item))
                {
                    foreach (PrivacyPattern pattern in Patterns)
                    {
                        if (pattern.Expression.IsMatch(field.Value ?? string.Empty))
                        {
                            findings.Add(
                                "Case=" + (item.CaseId ?? "<unknown>")
                                + "; Field=" + field.Name
                                + "; Pattern=" + pattern.Name
                                + "; Value=[REDACTED]");
                        }
                    }
                }
            }

            return new LocalModelAcceptancePrivacyValidationResult(findings);
        }

        private static IEnumerable<TextField> EnumerateTextFields(
            LocalSemanticAcceptanceCase item)
        {
            yield return new TextField("InputText", item.InputText);
            yield return new TextField("CandidateType", item.CandidateType);
            yield return new TextField("SubjectKey", item.SubjectKey);
            yield return new TextField("ExpectedSubjectKey", item.ExpectedSubjectKey);
            yield return new TextField("ExpectedCandidate", item.ExpectedCandidate);
            yield return new TextField("Notes", item.Notes);

            LocalSemanticSimulatedCandidate[] candidates =
                item.SimulatedCandidates ?? Array.Empty<LocalSemanticSimulatedCandidate>();
            for (int index = 0; index < candidates.Length; index++)
            {
                LocalSemanticSimulatedCandidate candidate = candidates[index];
                if (candidate == null)
                {
                    continue;
                }

                yield return new TextField(
                    "SimulatedCandidates[" + index + "].NormalizedCandidate",
                    candidate.NormalizedCandidate);
                yield return new TextField(
                    "SimulatedCandidates[" + index + "].DisplayValue",
                    candidate.DisplayValue);
            }
        }

        private sealed class PrivacyPattern
        {
            internal PrivacyPattern(string name, string pattern)
            {
                Name = name;
                Expression = new Regex(
                    pattern,
                    RegexOptions.CultureInvariant | RegexOptions.Compiled);
            }

            internal string Name { get; }

            internal Regex Expression { get; }
        }

        private sealed class TextField
        {
            internal TextField(string name, string value)
            {
                Name = name;
                Value = value;
            }

            internal string Name { get; }

            internal string Value { get; }
        }
    }

    internal sealed class LocalModelAcceptancePrivacyValidationResult
    {
        internal LocalModelAcceptancePrivacyValidationResult(IEnumerable<string> findings)
        {
            RedactedFindings = new ReadOnlyCollection<string>(findings.ToList());
        }

        internal bool IsSafe => RedactedFindings.Count == 0;

        internal IReadOnlyList<string> RedactedFindings { get; }
    }
}
