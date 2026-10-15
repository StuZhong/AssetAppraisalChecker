using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class SpellCheckResult
    {
        public SpellCheckResult(
            IEnumerable<TypoCandidate> candidates,
            SpellCheckDiagnostics diagnostics)
        {
            Candidates = new ReadOnlyCollection<TypoCandidate>(
                (candidates ?? throw new ArgumentNullException(nameof(candidates))).ToList());
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public IReadOnlyList<TypoCandidate> Candidates { get; }

        public SpellCheckDiagnostics Diagnostics { get; }
    }
}
