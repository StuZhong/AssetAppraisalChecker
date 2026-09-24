using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    public sealed class MoneyOccurrenceExtractor
    {
        private static readonly Regex BareTableNumberPattern = new Regex(
            @"^(?<leading>\s*)(?<number>(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)(?<trailing>\s*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly IReadOnlyList<MoneyOccurrence> NoOccurrences =
            new ReadOnlyCollection<MoneyOccurrence>(new MoneyOccurrence[0]);

        private readonly FieldClassifier _fieldClassifier = new FieldClassifier();

        public IReadOnlyList<MoneyOccurrence> Extract(DocumentReadResult documentReadResult)
        {
            if (documentReadResult == null)
            {
                throw new ArgumentNullException(nameof(documentReadResult));
            }

            if (!documentReadResult.IsSuccess)
            {
                return NoOccurrences;
            }

            return Extract(documentReadResult.Elements);
        }

        public IReadOnlyList<MoneyOccurrence> Extract(IEnumerable<DocumentElement> documentElements)
        {
            if (documentElements == null)
            {
                throw new ArgumentNullException(nameof(documentElements));
            }

            List<DocumentElement> elements = documentElements
                .OrderBy(element => element.DocumentOrderIndex)
                .ToList();
            var tableContextResolver = new TableBusinessContextResolver(elements);
            var occurrences = new List<MoneyOccurrence>();

            foreach (DocumentElement element in elements)
            {
                TableBusinessContext tableContext = tableContextResolver.Resolve(element);
                List<MoneyCandidate> candidates = MoneyCandidateExtractor.Extract(element.Text).ToList();
                var elementOccurrences = new List<MoneyOccurrence>();

                if (candidates.Count == 0)
                {
                    MoneyCandidate inheritedUnitCandidate;
                    if (TryCreateInheritedUnitCandidate(element, tableContext, out inheritedUnitCandidate))
                    {
                        candidates.Add(inheritedUnitCandidate);
                    }
                }

                foreach (MoneyCandidate candidate in candidates)
                {
                    MoneyParseResult parseResult = MoneyNormalizer.Parse(candidate.TextForParsing);
                    if (!parseResult.IsSuccess || !parseResult.NormalizedAmountYuan.HasValue)
                    {
                        continue;
                    }

                    string localContext = LocalContextBuilder.Build(element.Text, candidate);
                    FieldClassification classification = _fieldClassifier.Classify(
                        element,
                        localContext,
                        tableContext);
                    SubjectResolution subject = SubjectResolver.Resolve(
                        element,
                        localContext,
                        tableContext);
                    List<string> occurrenceEvidence = classification.Evidence.ToList();
                    if (!string.Equals(
                        candidate.RawText,
                        candidate.TextForParsing,
                        StringComparison.Ordinal))
                    {
                        occurrenceEvidence.Add(string.Format(
                            "当前表格存在明确金额单位“{0}”，该单位仅用于解释当前金额字段列中的裸数字。",
                            UnitText(tableContext.InheritedUnit)));
                    }

                    elementOccurrences.Add(new MoneyOccurrence(
                        candidate.RawText,
                        parseResult.NormalizedAmountYuan.Value,
                        parseResult.Status,
                        parseResult.NumberFormat,
                        parseResult.DetectedUnit,
                        parseResult.Warnings,
                        classification.Identity,
                        element.Location,
                        element.NearbyContext,
                        classification.Confidence,
                        classification.ValueCategoryConfidence,
                        classification.TaxBasisConfidence,
                        classification.ScopeConfidence,
                        occurrenceEvidence,
                        subject.SubjectKey,
                        subject.Confidence,
                        subject.Evidence,
                        candidate.StartIndex,
                        candidate.Length));
                }

                occurrences.AddRange(PairedTaxBasisSubjectPropagation.Apply(
                    element,
                    elementOccurrences));
            }

            return new ReadOnlyCollection<MoneyOccurrence>(occurrences);
        }

        private static bool TryCreateInheritedUnitCandidate(
            DocumentElement element,
            TableBusinessContext tableContext,
            out MoneyCandidate candidate)
        {
            candidate = null;
            if (element.ElementType != DocumentElementType.TableCell ||
                tableContext.InheritedUnit == MoneyUnit.Unknown ||
                !tableContext.HasExplicitMoneyFieldLabel)
            {
                return false;
            }

            Match match = BareTableNumberPattern.Match(element.Text ?? string.Empty);
            if (!match.Success)
            {
                return false;
            }

            Group number = match.Groups["number"];
            string textForParsing = number.Value + UnitText(tableContext.InheritedUnit);
            candidate = new MoneyCandidate(number.Value, number.Index, number.Length, textForParsing);
            return true;
        }

        private static string UnitText(MoneyUnit unit)
        {
            switch (unit)
            {
                case MoneyUnit.TenThousandYuan:
                    return "万元";
                case MoneyUnit.HundredMillionYuan:
                    return "亿元";
                case MoneyUnit.Yuan:
                    return "元";
                default:
                    throw new ArgumentOutOfRangeException(nameof(unit));
            }
        }
    }
}
