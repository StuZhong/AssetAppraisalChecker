using System;

namespace AssetAppraisalChecker.Semantic
{
    public sealed class SemanticCandidateSelectionContext
    {
        public SemanticCandidateSelectionContext(
            SemanticCandidateResolution resolution,
            bool shadowOnly)
        {
            if (!Enum.IsDefined(typeof(SemanticCandidateResolution), resolution))
            {
                throw new ArgumentOutOfRangeException(nameof(resolution));
            }

            Resolution = resolution;
            ShadowOnly = shadowOnly;
        }

        public SemanticCandidateResolution Resolution { get; }

        public bool ShadowOnly { get; }

        public bool BusinessSelectionAllowed =>
            Resolution == SemanticCandidateResolution.Resolvable && !ShadowOnly;
    }
}
