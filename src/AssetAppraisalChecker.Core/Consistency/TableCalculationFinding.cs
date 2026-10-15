using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.Consistency
{
    public enum TableCalculationKind { Increment, Rate }

    public sealed class TableCalculationFinding
    {
        internal TableCalculationFinding(TableCalculationKind kind, decimal calculatedValue,
            IEnumerable<TableCalculationValue> values)
        {
            Kind = kind;
            CalculatedValue = calculatedValue;
            Values = new ReadOnlyCollection<TableCalculationValue>(values.ToArray());
        }
        public TableCalculationKind Kind { get; }
        // Yuan for Increment; percentage points for Rate. Never a money-normalized percentage.
        public decimal CalculatedValue { get; }
        public IReadOnlyList<TableCalculationValue> Values { get; }
    }

    public sealed class TableCalculationValue
    {
        internal TableCalculationValue(DocumentElement element, string role, decimal value, decimal halfQuantum)
        {
            Element = element;
            Role = role;
            Value = value;
            HalfQuantum = halfQuantum;
        }
        public DocumentElement Element { get; }
        public string Role { get; }
        public decimal Value { get; }
        public decimal HalfQuantum { get; }
    }
}
