using System;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.CrossDocument
{
    public sealed class ConsistencyResultIdentity : IEquatable<ConsistencyResultIdentity>
    {
        public const string DefaultSubjectKey = "subject/v1:global";
        public const string DefaultResultRole = "PrimaryConsistency";
        public const string DefaultScopeKey = "scope/v1:default";

        private static readonly Regex SubjectKeyPattern = new Regex(
            "^subject/v[1-9][0-9]*:[a-z0-9]+(?:-[a-z0-9]+)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ScopeKeyPattern = new Regex(
            "^scope/v[1-9][0-9]*:[a-z0-9]+(?:-[a-z0-9]+)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ResultRolePattern = new Regex(
            "^[A-Z][A-Za-z0-9]*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public ConsistencyResultIdentity(
            FactType factType,
            string subjectKey,
            string resultRole,
            string scopeKey)
        {
            if (factType == FactType.Unknown || !Enum.IsDefined(typeof(FactType), factType))
            {
                throw new ArgumentOutOfRangeException(nameof(factType));
            }

            ValidateKey(subjectKey, SubjectKeyPattern, "SubjectKey", nameof(subjectKey));
            ValidateKey(scopeKey, ScopeKeyPattern, "ScopeKey", nameof(scopeKey));
            if (string.IsNullOrWhiteSpace(resultRole)
                || !string.Equals(resultRole, resultRole.Trim(), StringComparison.Ordinal)
                || !ResultRolePattern.IsMatch(resultRole))
            {
                throw new ArgumentException(
                    "ResultRole 必须是稳定的非本地化 PascalCase 代码。",
                    nameof(resultRole));
            }

            FactType = factType;
            SubjectKey = subjectKey;
            ResultRole = resultRole;
            ScopeKey = scopeKey;
        }

        public FactType FactType { get; }
        public string SubjectKey { get; }
        public string ResultRole { get; }
        public string ScopeKey { get; }

        public string CanonicalValue => string.Concat(
            FactType, "|", SubjectKey, "|", ResultRole, "|", ScopeKey);

        public static ConsistencyResultIdentity CreateDefault(FactType factType)
        {
            return new ConsistencyResultIdentity(
                factType,
                DefaultSubjectKey,
                DefaultResultRole,
                DefaultScopeKey);
        }

        public bool Equals(ConsistencyResultIdentity other)
        {
            return other != null
                && FactType == other.FactType
                && string.Equals(SubjectKey, other.SubjectKey, StringComparison.Ordinal)
                && string.Equals(ResultRole, other.ResultRole, StringComparison.Ordinal)
                && string.Equals(ScopeKey, other.ScopeKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ConsistencyResultIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)FactType;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(SubjectKey);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ResultRole);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ScopeKey);
                return hash;
            }
        }

        public override string ToString()
        {
            return CanonicalValue;
        }

        private static void ValidateKey(
            string value,
            Regex pattern,
            string displayName,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
                || !pattern.IsMatch(value))
            {
                throw new ArgumentException(
                    displayName + " 必须是版本化、标准化且不含敏感原文的稳定代码。",
                    parameterName);
            }
        }
    }
}
