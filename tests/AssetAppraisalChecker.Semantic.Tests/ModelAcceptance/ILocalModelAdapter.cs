namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal interface ILocalModelAdapter
    {
        string AdapterVersion { get; }

        LocalModelAdapterProfile Profile { get; }

        LocalModelAdapterObservation Analyze(LocalSemanticAcceptanceCase testCase);
    }
}
