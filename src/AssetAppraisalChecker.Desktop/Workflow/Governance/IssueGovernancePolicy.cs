using AssetAppraisalChecker.Core.Consistency;
using AssetAppraisalChecker.Mechanical;

namespace AssetAppraisalChecker.Desktop.Workflow.Governance
{
    internal static class IssueGovernancePolicy
    {
        public static IssueGovernanceClassification ForBusiness(IssueType issueType)
        {
            return new IssueGovernanceClassification(
                IssueCategory.Business,
                issueType == IssueType.BasicFieldParseFailed
                    ? IssueAuthority.ReviewSuggested
                    : IssueAuthority.Confirmed,
                issueType.ToString());
        }

        public static IssueGovernanceClassification ForCrossDocument(
            string governanceKind,
            bool isConfirmedConflict)
        {
            return new IssueGovernanceClassification(
                IssueCategory.Business,
                isConfirmedConflict
                    ? IssueAuthority.Confirmed
                    : IssueAuthority.ReviewSuggested,
                governanceKind);
        }

        public static IssueGovernanceClassification ForMechanical(MechanicalIssueKind kind)
        {
            switch (kind)
            {
                case MechanicalIssueKind.FontStyleOutlier:
                case MechanicalIssueKind.ParagraphFormatOutlier:
                    return new IssueGovernanceClassification(
                        IssueCategory.Formatting,
                        IssueAuthority.HighConfidence,
                        kind.ToString());

                case MechanicalIssueKind.DuplicateWord:
                case MechanicalIssueKind.DuplicateSentence:
                case MechanicalIssueKind.DuplicateParagraph:
                case MechanicalIssueKind.MissingEndingPunctuation:
                    return new IssueGovernanceClassification(
                        IssueCategory.Mechanical,
                        IssueAuthority.HighConfidence,
                        kind.ToString());

                default:
                    return new IssueGovernanceClassification(
                        IssueCategory.Mechanical,
                        IssueAuthority.Confirmed,
                        kind.ToString());
            }
        }
    }

    internal sealed class IssueGovernanceClassification
    {
        public IssueGovernanceClassification(
            IssueCategory category,
            IssueAuthority authority,
            string governanceKind)
        {
            Category = category;
            Authority = authority;
            GovernanceKind = governanceKind ?? string.Empty;
        }

        public IssueCategory Category { get; }
        public IssueAuthority Authority { get; }
        public string GovernanceKind { get; }
    }
}
