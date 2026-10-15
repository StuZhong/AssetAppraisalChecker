using System.Collections.Generic;

namespace AssetAppraisalChecker.Mechanical
{
    public interface IMechanicalDocumentChecker
    {
        string Name { get; }

        IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context);
    }
}
