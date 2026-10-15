using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Facts.Extraction
{
    public sealed class EvaluationMethodExtractionResult
    {
        public EvaluationMethodExtractionResult(
            IEnumerable<EvaluationMethodFact> facts,
            IEnumerable<EvaluationMethodStatement> statements)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (statements == null)
            {
                throw new ArgumentNullException(nameof(statements));
            }

            Facts = new ReadOnlyCollection<EvaluationMethodFact>(facts.ToList());
            Statements = new ReadOnlyCollection<EvaluationMethodStatement>(statements.ToList());
        }

        public IReadOnlyList<EvaluationMethodFact> Facts { get; }
        public IReadOnlyList<EvaluationMethodStatement> Statements { get; }
    }
}
