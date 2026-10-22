namespace AssetAppraisalChecker.Core.Consistency
{
    public enum IssueType
    {
        MoneyConflict = 1,
        BasicFieldConflict = 2,
        BasicFieldParseFailed = 3,
        ProjectNameExpectedMismatch = 4,
        EvaluationConclusionValidityAnomaly = 5,
        TableCalculationConflict = 6,
        MoneyComparisonIndeterminate = 7
    }
}
