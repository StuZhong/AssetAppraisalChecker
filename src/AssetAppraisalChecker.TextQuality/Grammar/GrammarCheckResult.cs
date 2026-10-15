using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.TextQuality.Grammar
{
    public sealed class GrammarCheckResult
    {
        public GrammarCheckResult(
            IEnumerable<ExpressionCandidate> candidates,
            GrammarCheckDiagnostics diagnostics)
        {
            Candidates = new ReadOnlyCollection<ExpressionCandidate>(
                (candidates ?? throw new ArgumentNullException(nameof(candidates))).ToList());
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public IReadOnlyList<ExpressionCandidate> Candidates { get; }
        public GrammarCheckDiagnostics Diagnostics { get; }
    }
}
