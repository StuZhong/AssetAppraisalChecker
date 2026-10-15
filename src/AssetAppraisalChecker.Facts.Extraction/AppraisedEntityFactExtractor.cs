using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Facts.Extraction
{
    public sealed class AppraisedEntityFactExtractor
    {
        // Only a self-contained, affirmative role assertion is authoritative.
        // No title, client, ownership, scope, or neighbouring-cell inference.
        private static readonly Regex Statement = new Regex(
            @"^(?:本次资产评估的|本次评估的|本次的|本次)?被评估单位\s*(?:为|是|：|:)\s*(?<value>.+)$",
            RegexOptions.CultureInvariant);
        private static readonly Regex CompanyName = new Regex(
            @"^(?!.*(?:公司.+公司|或者|以及|可能|待定|不确定|并非|不是|例如|假设|拟为))[A-Za-z0-9\u4e00-\u9fff·]{1,80}公司$", RegexOptions.CultureInvariant);
        private static readonly Regex NameAndDefinition = new Regex(
            @"^(?<name>[A-Za-z0-9\u4e00-\u9fff·]{1,80}公司)\s*[（(](?:以下简称|简称|下称)\s*[：:]?\s*[“""]?[A-Za-z0-9\u4e00-\u9fff·]{2,40}[”""]?[）)]$",
            RegexOptions.CultureInvariant);

        public IReadOnlyList<AppraisedEntityFact> Extract(DocumentReadResult document, string sourceDocument)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrWhiteSpace(sourceDocument))
                throw new ArgumentException("事实来源文档不能为空。", nameof(sourceDocument));
            var context = new AssessmentObjectSourceContext(document);
            var facts = new List<AppraisedEntityFact>();
            foreach (DocumentElement element in document.Elements.OrderBy(item => item.DocumentOrderIndex))
            // Do not truncate comma-separated names into an apparently unique
            // company. Unsupported compound statements remain unconfirmed.
            foreach (string clause in Regex.Split(element.Text, @"[。；;\r\n]"))
            {
                Match statement = Statement.Match(clause.Trim());
                if (!statement.Success) continue;
                string name = statement.Groups["value"].Value.Trim();
                Match definition = NameAndDefinition.Match(name);
                if (definition.Success) name = definition.Groups["name"].Value;
                IReadOnlyList<DocumentElement> evidence;
                string canonical = context.ResolveEntityName(name, out evidence);
                // A short name needs an explicit, unambiguous definition in this
                // document. An unresolved assertion invalidates the document set;
                // never silently drop it and compare a remaining competing name.
                if (canonical == null || (!CompanyName.IsMatch(name) && evidence.Count == 0)
                    || !CompanyName.IsMatch(canonical))
                    return new List<AppraisedEntityFact>().AsReadOnly();
                facts.Add(new AppraisedEntityFact(sourceDocument, element.Location, element.Text,
                    new FactConfidence(FactConfidenceBand.High, 0.95m), canonical, evidence));
            }
            return facts.AsReadOnly();
        }
    }
}
