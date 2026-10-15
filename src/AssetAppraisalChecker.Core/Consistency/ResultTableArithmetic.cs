using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.BusinessFields;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Core.Money;

namespace AssetAppraisalChecker.Core.Consistency
{
    // Deliberately narrow: one complete flat header, one row, one unambiguous value basis.
    // Merged/multi-level headers and competing method columns require further evidence.
    internal static class ResultTableArithmetic
    {
        private static readonly Regex Header = new Regex(
            @"^(?<role>账面价值|账面值|账面原值|账面净值|评估价值|评估值|评估原值|评估净值|增减值|增减额|增值额|增值率|增减率)(?:[（(](?<unit>亿元|万元|元|%|％)[）)])?$",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly Regex Number = new Regex(
            @"^\s*(?<sign>[-−－+]?)(?<number>(?:[0-9]{1,3}(?:,[0-9]{3})+|[0-9]+)(?:\.[0-9]{1,10})?)\s*(?<unit>亿元|万元|元|%|％)?\s*$",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        internal static IReadOnlyList<TableCalculationFinding> Evaluate(DocumentReadResult document, decimal tolerance)
        {
            var findings = new List<TableCalculationFinding>();
            if (!document.IsSuccess) return findings.AsReadOnly();
            var units = new TableBusinessContextResolver(document.Elements);
            foreach (var table in document.Elements.Where(e => e.ElementType == DocumentElementType.TableCell &&
                e.Location.TableIndex.HasValue).GroupBy(e => e.Location.TableIndex.Value))
            {
                var cells = table.ToArray();
                // Logical geometry must be explicitly known, never inferred from nearby numbers.
                if (cells.Any(e => !e.Location.RowIndex.HasValue || !e.Location.CellIndex.HasValue ||
                    !e.Location.TableGridColumnCount.HasValue || e.Location.LogicalGridSpan != 1 ||
                    e.Location.LogicalGridStart != e.Location.CellIndex)) continue;
                var rows = cells.GroupBy(e => e.Location.RowIndex.Value).OrderBy(r => r.Key).ToArray();
                var headers = rows.Where(r => r.Count(e => ReadHeader(e).Success) >= 3).ToArray();
                if (headers.Length != 1) continue;
                var headerCells = headers[0].OrderBy(e => e.Location.CellIndex).ToArray();
                if (!RectangularRow(headerCells)) continue;
                var mapped = headerCells.Select(e => new Column(e, ReadHeader(e))).Where(c => c.Match.Success).ToArray();
                var books = mapped.Where(c => c.Role.StartsWith("账面", StringComparison.Ordinal)).ToArray();
                var appraisals = mapped.Where(c => c.Role.StartsWith("评估", StringComparison.Ordinal)).ToArray();
                var increments = mapped.Where(c => c.Role == "增减值" || c.Role == "增减额" || c.Role == "增值额").ToArray();
                var rates = mapped.Where(c => c.Role == "增值率" || c.Role == "增减率").ToArray();
                if (books.Length != 1 || appraisals.Length != 1 || increments.Length != 1 || rates.Length > 1 ||
                    Basis(books[0].Role) != Basis(appraisals[0].Role)) continue;
                int firstValueColumn = mapped.Min(c => c.Index);
                foreach (var row in rows.Where(r => r.Key > headers[0].Key))
                {
                    var rowCells = row.OrderBy(e => e.Location.CellIndex).ToArray();
                    if (rowCells.Length != headerCells.Length || !RectangularRow(rowCells) ||
                        !rowCells.Take(firstValueColumn).Any(e => Regex.IsMatch(e.Text, @"[\p{L}]"))) continue;
                    try
                    {
                        EvaluateRow(rowCells, books[0], appraisals[0], increments[0], rates.SingleOrDefault(),
                            units, tolerance, findings);
                    }
                    catch (OverflowException)
                    {
                        // Out-of-range arithmetic is not proof of an incorrect business calculation.
                    }
                }
            }
            return findings.AsReadOnly();
        }

        private static void EvaluateRow(DocumentElement[] row, Column bookColumn, Column appraisalColumn,
            Column incrementColumn, Column rateColumn, TableBusinessContextResolver units,
            decimal tolerance, ICollection<TableCalculationFinding> findings)
        {
            var book = ReadValue(row[bookColumn.Index], bookColumn, units, false);
            var appraisal = ReadValue(row[appraisalColumn.Index], appraisalColumn, units, false);
            var increment = ReadValue(row[incrementColumn.Index], incrementColumn, units, false);
            if (book == null || appraisal == null || increment == null) return;
            decimal calculated = appraisal.Value - book.Value;
            // Each printed amount can be rounded by half its last displayed unit. This is
            // interval arithmetic, not a new arbitrary company rounding/tolerance rule.
            decimal uncertainty = appraisal.HalfQuantum + book.HalfQuantum + increment.HalfQuantum + tolerance;
            if (Math.Abs(calculated - increment.Value) > uncertainty)
            {
                findings.Add(new TableCalculationFinding(TableCalculationKind.Increment, calculated,
                    new[] { book, appraisal, increment }));
                return; // Do not cascade a bad numerator into a second rate issue.
            }
            if (rateColumn == null || book.Value <= book.HalfQuantum) return;
            var rate = ReadValue(row[rateColumn.Index], rateColumn, units, true);
            if (rate == null) return;
            decimal[] possible = {
                (increment.Value - increment.HalfQuantum) / (book.Value - book.HalfQuantum) * 100m,
                (increment.Value - increment.HalfQuantum) / (book.Value + book.HalfQuantum) * 100m,
                (increment.Value + increment.HalfQuantum) / (book.Value - book.HalfQuantum) * 100m,
                (increment.Value + increment.HalfQuantum) / (book.Value + book.HalfQuantum) * 100m };
            if (rate.Value + rate.HalfQuantum < possible.Min() || rate.Value - rate.HalfQuantum > possible.Max())
                findings.Add(new TableCalculationFinding(TableCalculationKind.Rate,
                    increment.Value / book.Value * 100m, new[] { book, appraisal, increment, rate }));
        }

        private static TableCalculationValue ReadValue(DocumentElement element, Column column,
            TableBusinessContextResolver units, bool percent)
        {
            Match match = Number.Match(element.Text);
            if (!match.Success) return null;
            string unitText = match.Groups["unit"].Value;
            decimal halfQuantum = 0.5m;
            string number = match.Groups["number"].Value;
            int decimalPoint = number.IndexOf('.');
            if (decimalPoint >= 0)
                for (int i = decimalPoint + 1; i < number.Length; i++) halfQuantum /= 10m;
            decimal value;
            if (percent)
            {
                if ((unitText.Length > 0 && unitText != "%" && unitText != "％") ||
                    (column.Unit.Length > 0 && column.Unit != "%" && column.Unit != "％") ||
                    (unitText.Length == 0 && column.Unit.Length == 0) ||
                    !decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out value)) return null;
            }
            else
            {
                if (unitText == "%" || unitText == "％" || column.Unit == "%" || column.Unit == "％") return null;
                if (unitText.Length > 0 && column.Unit.Length > 0 && unitText != column.Unit) return null;
                if (unitText.Length == 0) unitText = column.Unit;
                if (unitText.Length == 0) unitText = UnitName(units.Resolve(element).InheritedUnit);
                if (unitText.Length == 0) return null;
                var parsed = MoneyNormalizer.Parse(number + unitText);
                if (!parsed.IsSuccess) return null;
                value = parsed.NormalizedAmountYuan.Value;
                halfQuantum = MoneyUnitConverter.ConvertToYuan(halfQuantum, parsed.DetectedUnit);
            }
            if (match.Groups["sign"].Value.Length > 0 && match.Groups["sign"].Value != "+") value = -value;
            return new TableCalculationValue(element, column.Role, value, halfQuantum);
        }

        private static bool RectangularRow(DocumentElement[] cells) =>
            cells.Length > 0 && cells.All(e => e.Location.TableGridColumnCount == cells.Length) &&
            cells.Select(e => e.Location.CellIndex.Value).SequenceEqual(Enumerable.Range(0, cells.Length));
        private static Match ReadHeader(DocumentElement e) => Header.Match(Regex.Replace(e.Text, @"\s", ""));
        private static string Basis(string role) => role.EndsWith("原值", StringComparison.Ordinal) ? "原值" :
            role.EndsWith("净值", StringComparison.Ordinal) ? "净值" : "价值";
        private static string UnitName(MoneyUnit unit) => unit == MoneyUnit.Yuan ? "元" :
            unit == MoneyUnit.TenThousandYuan ? "万元" : unit == MoneyUnit.HundredMillionYuan ? "亿元" : "";
        private sealed class Column
        {
            internal Column(DocumentElement element, Match match)
            {
                Index = element.Location.CellIndex.Value;
                Match = match;
                Role = match.Groups["role"].Value;
                Unit = match.Groups["unit"].Value;
            }
            internal int Index { get; }
            internal Match Match { get; }
            internal string Role { get; }
            internal string Unit { get; }
        }
    }
}
