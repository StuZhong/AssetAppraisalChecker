namespace AssetAppraisalChecker.TextQuality.Grammar
{
    public sealed class GrammarCheckDiagnostics
    {
        public GrammarCheckDiagnostics(
            int paragraphsScanned,
            int sentencesScanned,
            int bodySentences,
            int rulesEvaluated,
            int candidates,
            int reportedIssues,
            int skippedHeadings,
            int skippedTables,
            int protectedSpanCount)
        {
            ParagraphsScanned = paragraphsScanned;
            SentencesScanned = sentencesScanned;
            BodySentences = bodySentences;
            RulesEvaluated = rulesEvaluated;
            Candidates = candidates;
            ReportedIssues = reportedIssues;
            SkippedHeadings = skippedHeadings;
            SkippedTables = skippedTables;
            ProtectedSpanCount = protectedSpanCount;
        }

        public int ParagraphsScanned { get; }
        public int SentencesScanned { get; }
        public int BodySentences { get; }
        public int RulesEvaluated { get; }
        public int Candidates { get; }
        public int ReportedIssues { get; }
        public int SkippedHeadings { get; }
        public int SkippedTables { get; }
        public int ProtectedSpanCount { get; }
    }
}
