using System;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Mechanical.Units
{
    public sealed class UnitRelation
    {
        public UnitRelation(
            UnitExpression quantityUnit,
            UnitExpression unitPrice,
            DocumentElement quantityElement,
            DocumentElement priceElement,
            bool hasExplicitBinding = false)
        {
            QuantityUnit = quantityUnit ?? throw new ArgumentNullException(nameof(quantityUnit));
            UnitPrice = unitPrice ?? throw new ArgumentNullException(nameof(unitPrice));
            QuantityElement = quantityElement ?? throw new ArgumentNullException(nameof(quantityElement));
            PriceElement = priceElement ?? throw new ArgumentNullException(nameof(priceElement));
            HasExplicitBinding = hasExplicitBinding;
        }

        public UnitExpression QuantityUnit { get; }

        public UnitExpression UnitPrice { get; }

        public DocumentElement QuantityElement { get; }

        public DocumentElement PriceElement { get; }
        public bool HasExplicitBinding { get; }

        public bool IsComparable =>
            !QuantityUnit.IsCompound &&
            UnitPrice.IsCompound &&
            QuantityUnit.Family != UnitFamily.Unknown &&
            UnitPrice.NumeratorFamily == UnitFamily.Currency &&
            UnitPrice.DenominatorFamily != UnitFamily.Unknown;

        public bool IsConsistent =>
            IsComparable && QuantityUnit.Family == UnitPrice.DenominatorFamily;
    }
}
