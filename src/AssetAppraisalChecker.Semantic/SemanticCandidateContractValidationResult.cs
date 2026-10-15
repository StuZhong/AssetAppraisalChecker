using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateContractValidationResult
    {
        public SemanticCandidateContractValidationResult(
            bool isValid,
            IEnumerable<SemanticCandidateContractValidationReasonCode> reasonCodes)
        {
            if (reasonCodes == null)
            {
                throw new ArgumentNullException(nameof(reasonCodes));
            }

            List<SemanticCandidateContractValidationReasonCode> items = reasonCodes
                .Distinct()
                .OrderBy(item => item)
                .ToList();
            if (items.Count == 0)
            {
                throw new ArgumentException(
                    "Contract Validation 必须包含 ReasonCode。",
                    nameof(reasonCodes));
            }

            if (isValid != (items.Count == 1
                && items[0] == SemanticCandidateContractValidationReasonCode.Valid))
            {
                throw new ArgumentException(
                    "IsValid 与 ReasonCode 不一致。",
                    nameof(reasonCodes));
            }

            IsValid = isValid;
            ReasonCodes = new ReadOnlyCollection<SemanticCandidateContractValidationReasonCode>(
                items);
        }

        public bool IsValid { get; }

        public IReadOnlyList<SemanticCandidateContractValidationReasonCode> ReasonCodes { get; }
    }
}
