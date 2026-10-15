using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateSet
    {
        public SemanticCandidateSet(
            SemanticCandidateSetIdentity identity,
            IEnumerable<SemanticCandidate> candidates)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            List<SemanticCandidate> items = candidates.ToList();
            if (items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "CandidateSet 不能包含 null。",
                    nameof(candidates));
            }

            if (items.Any(item => !item.Identity.SetIdentity.Equals(identity)))
            {
                throw new ArgumentException(
                    "Candidate 必须属于当前 CandidateSet Identity。",
                    nameof(candidates));
            }

            if (items.GroupBy(item => item.Identity).Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "CandidateSet 不能包含重复 CandidateIdentity。",
                    nameof(candidates));
            }

            items.Sort((left, right) => string.Compare(
                left.Identity.CanonicalValue,
                right.Identity.CanonicalValue,
                StringComparison.Ordinal));
            Candidates = new ReadOnlyCollection<SemanticCandidate>(items);
        }

        public SemanticCandidateSetIdentity Identity { get; }

        public IReadOnlyList<SemanticCandidate> Candidates { get; }
    }
}
