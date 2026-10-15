using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Mechanical.Units;

namespace AssetAppraisalChecker.Mechanical
{
    public sealed class MechanicalDocumentCheckPipeline
    {
        private readonly IReadOnlyList<IMechanicalDocumentChecker> _checkers;

        public MechanicalDocumentCheckPipeline()
            : this(new IMechanicalDocumentChecker[]
            {
                new DuplicateContentChecker(),
                new WhitespaceAndCharacterChecker(),
                new PunctuationChecker(),
                new SequenceChecker(),
                new DocumentResidueChecker(),
                new FontStyleChecker(),
                new ParagraphFormatChecker(),
                new UnitConsistencyChecker()
            })
        {
        }

        public MechanicalDocumentCheckPipeline(IEnumerable<IMechanicalDocumentChecker> checkers)
        {
            List<IMechanicalDocumentChecker> values =
                (checkers ?? throw new ArgumentNullException(nameof(checkers))).ToList();
            if (values.Count == 0 || values.Any(item => item == null))
            {
                throw new ArgumentException("机械检查器集合不能为空且不能包含 null。", nameof(checkers));
            }

            _checkers = new ReadOnlyCollection<IMechanicalDocumentChecker>(values);
        }

        public IReadOnlyList<MechanicalIssue> Check(MechanicalCheckContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new ReadOnlyCollection<MechanicalIssue>(
                _checkers.SelectMany(item => item.Check(context)).ToList());
        }
    }
}
