using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Facts.Extraction
{
    // Role boundaries and explicit document-local name definitions are separate:
    // a name definition can resolve identity, but never establishes a business role.
    internal sealed class AssessmentObjectSourceContext
    {
        private static readonly Regex ObjectLabel = new Regex(
            @"(?:本次)?评估对象\s*(?:为|是|：|:)", RegexOptions.CultureInvariant);
        private static readonly Regex OtherRoleLabel = new Regex(
            @"(?:委托人|被评估单位|被评估企业|评估范围|项目名称)\s*(?:为|是|：|:)", RegexOptions.CultureInvariant);
        private static readonly Regex OwnerLabel = new Regex(
            @"(?:产权持有人|所有权人|权利人)\s*(?:为|是|：|:)", RegexOptions.CultureInvariant);
        private static readonly Regex DefinitionRolePrefix = new Regex(
            @"^(?:(?:本次资产评估的|本次评估的|本次的|本次)?被评估单位|委托人|被评估企业|产权持有人|所有权人|权利人)\s*(?:为|是|：|:)\s*",
            RegexOptions.CultureInvariant);
        private static readonly Regex AliasDefinition = new Regex(
            @"^(?<full>[A-Za-z0-9\u4e00-\u9fff·]{1,80}?(?:公司|单位))\s*[（(](?:以下简称|简称|下称)\s*[：:]?\s*[“""]?(?<alias>[A-Za-z0-9\u4e00-\u9fff·]{2,40})[”""]?[）)]\s*$",
            RegexOptions.CultureInvariant);
        private readonly Dictionary<string, HashSet<string>> aliases =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<DocumentElement>> aliasEvidence =
            new Dictionary<string, List<DocumentElement>>(StringComparer.Ordinal);

        public AssessmentObjectSourceContext(DocumentReadResult document)
        {
            foreach (DocumentElement element in document.Elements)
            foreach (string clause in Regex.Split(element.Text ?? string.Empty, @"[，。；;\r\n]"))
            {
                // Only self-contained explicit definitions in this document; no fuzzy,
                // containment or cross-document entity inference.
                string definition = DefinitionRolePrefix.Replace(clause.Trim(), string.Empty);
                Match match = AliasDefinition.Match(definition);
                if (!match.Success) continue;
                string alias = match.Groups["alias"].Value;
                string full = match.Groups["full"].Value;
                if (alias == full) continue;
                HashSet<string> names;
                if (!aliases.TryGetValue(alias, out names))
                    aliases.Add(alias, names = new HashSet<string>(StringComparer.Ordinal));
                names.Add(full);
                List<DocumentElement> evidence;
                if (!aliasEvidence.TryGetValue(alias, out evidence))
                    aliasEvidence.Add(alias, evidence = new List<DocumentElement>());
                evidence.Add(element);
            }
        }

        public string ResolveEntityName(string entityName, out IReadOnlyList<DocumentElement> evidence)
        {
            var definitions = new List<DocumentElement>();
            evidence = definitions.AsReadOnly();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> names;
            while (aliases.TryGetValue(entityName, out names))
            {
                if (names.Count != 1 || !visited.Add(entityName)) return null;
                definitions.AddRange(aliasEvidence[entityName]);
                entityName = names.Single();
            }
            return entityName;
        }

        public static string SelectObjectStatement(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            MatchCollection labels = ObjectLabel.Matches(text);
            if (labels.Count > 1) return null; // Current extractor cannot bind multiple objects.
            if (labels.Count == 0)
            {
                // A complete existing explicit ownership statement is still eligible.
                // A project/client/evaluated-entity statement is not a substitute.
                return OwnerLabel.IsMatch(text) && !OtherRoleLabel.IsMatch(text) ? text : null;
            }

            int start = labels[0].Index;
            string prefix = text.Substring(Math.Max(0, start - 8), Math.Min(8, start));
            if (Regex.IsMatch(prefix, @"(?:不|未|非|不是|不属于)\s*(?:本次)?$")) return null;
            string statement = text.Substring(start);
            int end = statement.IndexOfAny(new[] { '。', '；', ';', '\r', '\n' });
            if (end >= 0) statement = statement.Substring(0, end);
            Match otherRole = OtherRoleLabel.Match(statement);
            if (otherRole.Success) statement = statement.Substring(0, otherRole.Index);
            return statement;
        }
    }
}
