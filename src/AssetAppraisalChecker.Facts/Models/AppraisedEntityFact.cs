using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models
{
    // A separate party-role fact, not an AssessmentObjectFact.Owner. The existing
    // object checker hosts this result under its own stable ResultRole.
    public sealed class AppraisedEntityFact : BusinessFact
    {
        public const string ResultRole = "AppraisedEntityConsistency";

        public AppraisedEntityFact(string sourceDocument, DocumentLocation sourceLocation,
            string originalText, FactConfidence confidence, string companyName,
            IEnumerable<DocumentElement> aliasEvidence)
            : base(FactType.AssessmentObject, sourceDocument, sourceLocation,
                originalText, confidence, companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName))
                throw new ArgumentException("被评估单位名称不能为空。", nameof(companyName));
            if (aliasEvidence == null) throw new ArgumentNullException(nameof(aliasEvidence));
            var evidence = aliasEvidence.ToArray();
            if (evidence.Any(item => item == null))
                throw new ArgumentException("简称证据不能包含空项。", nameof(aliasEvidence));
            CompanyName = companyName;
            AliasEvidence = new ReadOnlyCollection<DocumentElement>(evidence);
        }

        public string CompanyName { get; }
        public IReadOnlyList<DocumentElement> AliasEvidence { get; }
    }
}
