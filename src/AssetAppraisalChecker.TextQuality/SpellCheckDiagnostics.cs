namespace AssetAppraisalChecker.TextQuality
{
    public sealed class SpellCheckDiagnostics
    {
        public SpellCheckDiagnostics(
            int totalChineseTextSpans,
            int protectedSpans,
            int exactLexiconHits,
            int knownTypoHits,
            int nearMatchCandidates,
            int reportedIssues)
        {
            TotalChineseTextSpans = totalChineseTextSpans;
            ProtectedSpans = protectedSpans;
            ExactLexiconHits = exactLexiconHits;
            KnownTypoHits = knownTypoHits;
            NearMatchCandidates = nearMatchCandidates;
            ReportedIssues = reportedIssues;
        }

        public int TotalChineseTextSpans { get; }
        public int ProtectedSpans { get; }
        public int ExactLexiconHits { get; }
        public int KnownTypoHits { get; }
        public int NearMatchCandidates { get; }
        public int ReportedIssues { get; }
    }
}
