using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Desktop.Workflow.Governance;
using AssetAppraisalChecker.TextQuality.Grammar;
using AssetAppraisalChecker.Mechanical;

namespace AssetAppraisalChecker.Desktop.ViewModels
{
    // Presentation priority only. Neither an issue's authority/severity nor its business result changes.
    public static class IssueAttentionPolicy
    {
        private static readonly HashSet<string> ControlledLanguageKinds = new HashSet<string>(
            ExpressionRuleCatalog.Default.Rules.Select(rule => "Grammar." + rule.Kind + "." + rule.RuleId),
            StringComparer.Ordinal)
        {
            "Spell.KnownTypo",
            "Spell.ConfusableProfessionalTerm"
        };

        public static bool IsAuxiliary(IssueViewModel issue)
        {
            if (issue == null) return false;

            // Admission is based on rule provenance, not severity or the customer's file.
            // Peer-majority evidence is not an authoritative formatting specification.
            if (issue.Category == IssueCategory.Formatting) return true;
            if (issue.Category == IssueCategory.Language)
                return !ControlledLanguageKinds.Contains(issue.GovernanceKind);

            if (issue.Category == IssueCategory.Business)
            {
                // Only the new explicit gross-anomaly rule is admitted here; existing rules are unchanged.
                if (issue.IssueType == IssueType.EvaluationConclusionValidityAnomaly)
                    return issue.Authority == IssueAuthority.ReviewSuggested;
                return issue.Authority != IssueAuthority.Confirmed ||
                    (issue.IssueType != IssueType.MoneyConflict &&
                     issue.IssueType != IssueType.TableCalculationConflict &&
                     issue.IssueType != IssueType.BasicFieldConflict &&
                     issue.IssueType != IssueType.ProjectNameExpectedMismatch);
            }

            if (issue.Category != IssueCategory.Mechanical)
                return issue.Authority != IssueAuthority.Confirmed;

            switch (issue.GovernanceKind)
            {
                case "DuplicateWord":
                case "UnpairedChineseBracket":
                case "UnpairedChineseQuote":
                case "UnpairedBookTitle":
                case "UnitConsistency":
                    return issue.Authority == IssueAuthority.ReviewSuggested ||
                        !issue.Occurrences.Any(occurrence => occurrence.SourceOccurrence.Evidence
                            .Contains(MechanicalEvidenceStrength.HardDeterministic, StringComparer.Ordinal));
                case "DuplicateSentence":
                case "DuplicateParagraph":
                case "RepeatedPunctuation":
                case "SequenceJump":
                case "SequenceDuplicate":
                case "SequenceReverse":
                case "ControlCharacter":
                case "CommentResidue":
                case "InsertedRevision":
                case "DeletedRevision":
                    return issue.Authority == IssueAuthority.ReviewSuggested;
                default:
                    // Whitespace, missing sentence endings, style hints and unknown
                    // rules remain available in Detailed; they are not repaired/deleted.
                    return true;
            }
        }
    }
}
