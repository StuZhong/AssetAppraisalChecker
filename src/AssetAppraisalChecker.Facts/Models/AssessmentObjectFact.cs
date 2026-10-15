using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Models
{
    public enum AssessmentEquityInterest
    {
        EntireShareholdersEquity,
        PartialShareholding
    }

    public sealed class AssessmentObjectFact : BusinessFact
    {
        public AssessmentObjectFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence)
            : base(FactType.AssessmentObject, sourceDocument, sourceLocation, originalText, confidence)
        {
        }

        public AssessmentObjectFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string value)
            : base(
                FactType.AssessmentObject,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence,
                value)
        {
        }

        public AssessmentObjectFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string assetType,
            string owner,
            string economicAction,
            decimal quantity,
            string scope)
            : this(sourceDocument, sourceLocation, originalText, confidence,
                assetType, owner, economicAction, quantity, scope, null)
        {
        }

        public AssessmentObjectFact(
            string sourceDocument,
            DocumentLocation sourceLocation,
            string originalText,
            FactConfidence confidence,
            string assetType,
            string owner,
            string economicAction,
            decimal quantity,
            string scope,
            IEnumerable<DocumentElement> ownerAliasEvidence)
            : base(
                FactType.AssessmentObject,
                sourceDocument,
                sourceLocation,
                originalText,
                confidence)
        {
            AssetType = RequireValue(assetType, nameof(assetType), "资产类型不能为空。");
            Owner = RequireValue(owner, nameof(owner), "权利人不能为空。");
            EconomicAction = RequireValue(
                economicAction,
                nameof(economicAction),
                "经济行为不能为空。");

            if (quantity < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "评估对象数量不能为负数。");
            }

            Quantity = quantity;
            Scope = RequireValue(scope, nameof(scope), "评估对象范围不能为空。");
            OwnerAliasEvidence = new ReadOnlyCollection<DocumentElement>(
                (ownerAliasEvidence ?? Enumerable.Empty<DocumentElement>()).ToList());
        }

        // Equity is an independent object identity, not a made-up asset quantity
        // or an ownership/economic-action assertion.
        public AssessmentObjectFact(
            string sourceDocument, DocumentLocation sourceLocation, string originalText,
            FactConfidence confidence, string equitySubject, AssessmentEquityInterest equityInterest,
            decimal equityPercentage, IEnumerable<DocumentElement> equitySubjectAliasEvidence)
            : base(FactType.AssessmentObject, sourceDocument, sourceLocation, originalText, confidence,
                EquityValue(equitySubject, equityInterest, equityPercentage))
        {
            EquitySubject = equitySubject;
            EquityInterest = equityInterest;
            EquityPercentage = equityPercentage;
            EquitySubjectAliasEvidence = new ReadOnlyCollection<DocumentElement>(
                (equitySubjectAliasEvidence ?? Enumerable.Empty<DocumentElement>()).ToList());
        }

        public bool IsEquity => EquityPercentage.HasValue;
        public string EquitySubject { get; }
        public AssessmentEquityInterest? EquityInterest { get; }
        public decimal? EquityPercentage { get; }
        public IReadOnlyList<DocumentElement> EquitySubjectAliasEvidence { get; } =
            new ReadOnlyCollection<DocumentElement>(new DocumentElement[0]);

        private static string EquityValue(string subject, AssessmentEquityInterest interest, decimal percentage)
        {
            RequireValue(subject, nameof(subject), "股权类评估对象主体不能为空。");
            if (!Enum.IsDefined(typeof(AssessmentEquityInterest), interest))
                throw new ArgumentOutOfRangeException(nameof(interest));
            if (interest == AssessmentEquityInterest.EntireShareholdersEquity ? percentage != 100m
                : percentage <= 0m || percentage >= 100m)
                throw new ArgumentOutOfRangeException(nameof(percentage), "权益类型与股权比例必须明确且一致。");
            return string.Concat("Equity|", subject.Length.ToString(CultureInfo.InvariantCulture), ":", subject,
                "|", interest, "|", percentage.ToString("G29", CultureInfo.InvariantCulture));
        }

        public string AssetType { get; }

        public string Owner { get; }

        public string EconomicAction { get; }

        public decimal? Quantity { get; }

        public string Scope { get; }

        // Supporting explicit definitions retain their own structural locations.
        // The main SourceLocation/OriginalText still points to the object assertion.
        public IReadOnlyList<DocumentElement> OwnerAliasEvidence { get; } =
            new ReadOnlyCollection<DocumentElement>(new DocumentElement[0]);

        private static string RequireValue(string value, string parameterName, string message)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(message, parameterName);
            }

            return value;
        }
    }
}
