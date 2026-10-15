using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.ReportValidation
{
    /// <summary>
    /// Narrow gross-anomaly screening, not a company tolerance or a legal compliance verdict.
    /// Only explicit conclusion-use ranges tied to one unambiguous benchmark are evaluated.
    /// </summary>
    public sealed class EvaluationConclusionValidityChecker
    {
        private const string DateToken = @"(?:[0-9]{4}年[0-9]{1,2}月[0-9]{1,2}日|[0-9]{4}[-/.][0-9]{1,2}[-/.][0-9]{1,2})(?![0-9])";
        private static readonly Regex Benchmark = Pattern(@"评估基准日\s*(?:为|是|[:：])?\s*(?<date>" + DateToken + ")");
        private static readonly Regex Label = Pattern(@"评估结论(?:的)?(?:使用)?有效期");
        private static readonly Regex Heading = Pattern(@"^(?:[一二三四五六七八九十百0-9（）()、.．\s]+)?评估结论(?:的)?(?:使用)?有效期\s*[:：]?\s*$");
        private static readonly Regex Continuation = Pattern(@"^\s*(?:本次评估结论的)?(?:使用)?有效期(?:为|[:：])");
        private static readonly Regex Range = Pattern(@"自\s*(?:评估基准日\s*)?(?<start>" + DateToken +
            @")\s*(?:起\s*)?至\s*(?<end>" + DateToken + @")\s*(?:止|有效)?");
        private static readonly Regex OtherOrUncertainScope = Pattern(
            "许可证|合同|营业执照|资质|证书|授权|租赁|法规有效|例如|示例|假设|历史|上次|原报告|不适用|另有约定");

        public EvaluationConclusionValidityResult Check(IEnumerable<DocumentElement> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var elements = source.OrderBy(item => item.DocumentOrderIndex).ToList();
            var benchmarks = new List<ValidityDateEvidence>();
            var ranges = new List<Tuple<ValidityDateEvidence, ValidityDateEvidence>>();
            bool invalidExplicitDate = false;

            for (int i = 0; i < elements.Count; i++)
            {
                DocumentElement element = elements[i];
                foreach (Match match in Benchmark.Matches(element.Text))
                {
                    // Historical/example dates and other documents' dates are not authoritative.
                    string sentence = SentenceAt(element.Text, match.Index);
                    if (OtherOrUncertainScope.IsMatch(sentence)) continue;
                    ValidityDateEvidence date = ReadDate(element, match.Groups["date"], 0);
                    if (date == null) invalidExplicitDate = true;
                    else benchmarks.Add(date);
                }

                MatchCollection labels = Label.Matches(element.Text);
                // Do not take the first range from a paragraph comparing old/new or other scopes.
                if (labels.Count > 1) return Unknown();
                Match label = Label.Match(element.Text);
                int offset;
                string clause;
                if (label.Success && !Heading.IsMatch(element.Text))
                {
                    offset = label.Index;
                    clause = element.Text.Substring(offset).Split('。', '；', ';', '\n', '\r')[0];
                    if (OtherOrUncertainScope.IsMatch(SentenceAt(element.Text, label.Index))) continue;
                }
                else if (i > 0 && Heading.IsMatch(elements[i - 1].Text) &&
                    AdjacentBodyParagraphs(elements[i - 1], element) && Continuation.IsMatch(element.Text))
                {
                    offset = 0;
                    clause = element.Text.Split('。', '；', ';', '\n', '\r')[0];
                }
                else continue;

                if (OtherOrUncertainScope.IsMatch(clause)) continue;
                MatchCollection matches = Range.Matches(clause);
                // Multiple scopes/ranges in one clause must never be guessed.
                if (matches.Count != 1 || Regex.Matches(clause, DateToken).Count != 2) continue;
                ValidityDateEvidence start = ReadDate(element, matches[0].Groups["start"], offset);
                ValidityDateEvidence end = ReadDate(element, matches[0].Groups["end"], offset);
                if (start == null || end == null) invalidExplicitDate = true;
                else ranges.Add(Tuple.Create(start, end));
            }

            // Conflicting benchmarks or competing conclusion ranges abstain, not majority voting.
            if (invalidExplicitDate || benchmarks.Select(item => item.Date).Distinct().Count() != 1 ||
                ranges.Select(item => Tuple.Create(item.Item1.Date, item.Item2.Date)).Distinct().Count() != 1)
                return Unknown();

            var benchmark = benchmarks[0];
            var period = ranges[0];
            DateTime startDate = period.Item1.Date, endDate = period.Item2.Date;
            // The current templates use the benchmark itself; next-day starts are also unambiguous.
            // Other start/benchmark relationships require a confirmed rule before judging them.
            if (startDate != benchmark.Date &&
                (benchmark.Date == DateTime.MaxValue.Date || startDate != benchmark.Date.AddDays(1)))
                return Unknown();

            EvaluationConclusionValidityAnomaly? anomaly = null;
            if (endDate < startDate) anomaly = EvaluationConclusionValidityAnomaly.ReversedRange;
            else if (startDate.Year < 9999 && endDate <= startDate.AddMonths(1))
                anomaly = EvaluationConclusionValidityAnomaly.ClearlyShortPeriod;
            // Inclusive end dates: Sep 30 to Sep 29 two years later is still a two-year period.
            else if (startDate.Year <= 9997 && endDate >= startDate.AddYears(2).AddDays(-1))
                anomaly = EvaluationConclusionValidityAnomaly.ClearlyLongPeriod;

            return new EvaluationConclusionValidityResult(true, anomaly.HasValue
                ? new[] { new EvaluationConclusionValidityFinding(anomaly.Value, benchmark, period.Item1, period.Item2) }
                : new EvaluationConclusionValidityFinding[0]);
        }

        private static bool AdjacentBodyParagraphs(DocumentElement before, DocumentElement after)
        {
            return !before.Location.TableIndex.HasValue && !after.Location.TableIndex.HasValue &&
                before.Location.DocumentPart == after.Location.DocumentPart &&
                before.Location.ParagraphIndex.HasValue && after.Location.ParagraphIndex == before.Location.ParagraphIndex + 1;
        }

        private static string SentenceAt(string text, int position)
        {
            char[] boundaries = { '。', '；', ';', '\n', '\r' };
            int start = text.LastIndexOfAny(boundaries, position);
            int end = text.IndexOfAny(boundaries, position);
            return text.Substring(start + 1, (end < 0 ? text.Length : end) - start - 1);
        }

        private static ValidityDateEvidence ReadDate(DocumentElement element, Group group, int offset)
        {
            var result = BasicFieldNormalizer.Normalize(BasicFieldType.ValuationDate, group.Value);
            return result.IsSuccess && result.NormalizedValue.DateValue.HasValue
                ? new ValidityDateEvidence(element, group.Value, offset + group.Index, result.NormalizedValue.DateValue.Value)
                : null;
        }

        private static EvaluationConclusionValidityResult Unknown() =>
            new EvaluationConclusionValidityResult(false, new EvaluationConclusionValidityFinding[0]);

        private static Regex Pattern(string expression) => new Regex(expression,
            RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
