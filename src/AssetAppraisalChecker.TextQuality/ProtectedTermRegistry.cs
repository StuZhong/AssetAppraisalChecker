using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.BasicFields;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.TextQuality
{
    public sealed class ProtectedTermRegistry
    {
        private static readonly IReadOnlyList<ProtectionPattern> Patterns =
            new ReadOnlyCollection<ProtectionPattern>(new[]
            {
                new ProtectionPattern("Url", @"(?:https?://|www\.)[^\s，。；]+"),
                new ProtectionPattern("Email", @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}"),
                new ProtectionPattern("ReportNumber", @"[^\s，。；：:]{0,20}评报字[〔\[\(（【]?\d{4}[〕\]\)）】]?第?\d+号"),
                new ProtectionPattern("Date", @"\d{4}(?:年|[-/.])\d{1,2}(?:月|[-/.])\d{1,2}日?"),
                new ProtectionPattern("MoneyOrUnit", @"\d[\d,]*(?:\.\d+)?\s*(?:人民币)?(?:万元|元|㎡|m²|m2|m³|m3|kg|公斤|千克|吨|台|个|件|套|%|％)(?:\s*(?:/|／|每)\s*(?:㎡|m²|m2|m³|m3|kg|公斤|千克|吨|台|个|件|套|月|年))?"),
                new ProtectionPattern("AssessmentObject", @"(?:供配电资产|输配电资产|房屋建筑物|机器设备|土地使用权|整体资产组|企业整体|单项资产)"),
                new ProtectionPattern("Organization", @"[一-龥A-Za-z0-9（）()·]{2,40}(?:有限责任公司|集团有限公司|股份有限公司|有限公司)"),
                new ProtectionPattern("PersonName", @"(?:法定代表人|联系人|委托人)\s*[：:]\s*[一-龥·]{2,8}"),
                new ProtectionPattern("EquipmentModel", @"(?<![A-Za-z0-9])[A-Za-z]{2,}[A-Za-z0-9_.]*[-_][A-Za-z0-9_.-]*\d[A-Za-z0-9_.-]*(?![A-Za-z0-9])"),
                new ProtectionPattern("AlphaNumericCode", @"(?<![A-Za-z0-9])[A-Za-z]+\d+[A-Za-z0-9_.-]*(?![A-Za-z0-9])"),
                new ProtectionPattern("LegalOrSerialNumber", @"第[一二三四五六七八九十百千万\d]+号")
            });

        private static readonly Regex ExplicitProtectedValue = new Regex(
            @"(?:项目名称|评估对象|资产名称|设备型号|地址)\s*[：:]\s*(?<value>[^，。；\r\n]{2,80})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly IReadOnlyDictionary<int, IReadOnlyList<ProtectedTerm>> _byElement;

        private ProtectedTermRegistry(IEnumerable<ProtectedTerm> terms)
        {
            Terms = new ReadOnlyCollection<ProtectedTerm>(
                terms.OrderBy(item => item.DocumentOrderIndex)
                    .ThenBy(item => item.Span.Start)
                    .ThenBy(item => item.Span.Length)
                    .ToList());
            _byElement = Terms
                .GroupBy(item => item.DocumentOrderIndex)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ProtectedTerm>)new ReadOnlyCollection<ProtectedTerm>(
                        group.ToList()));
        }

        public IReadOnlyList<ProtectedTerm> Terms { get; }

        public static ProtectedTermRegistry Build(DocumentReadResult document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var terms = new List<ProtectedTerm>();
            foreach (DocumentElement element in document.Elements)
            {
                string text = element.Text ?? string.Empty;
                foreach (ProtectionPattern pattern in Patterns)
                {
                    AddMatches(terms, element, text, pattern.Kind, pattern.Regex.Matches(text));
                }

                AddMatches(
                    terms,
                    element,
                    text,
                    "ExplicitBusinessValue",
                    ExplicitProtectedValue.Matches(text),
                    "value");
            }

            AddExistingBasicFields(document, terms);
            return new ProtectedTermRegistry(Deduplicate(terms));
        }

        public bool IsProtected(DocumentElement element, TextSpan span)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }

            return _byElement.TryGetValue(
                    element.DocumentOrderIndex,
                    out IReadOnlyList<ProtectedTerm> values) &&
                values.Any(item => item.Span.Overlaps(span));
        }

        public bool IsFullyProtected(DocumentElement element, TextSpan span)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }

            return _byElement.TryGetValue(
                    element.DocumentOrderIndex,
                    out IReadOnlyList<ProtectedTerm> values) &&
                values.Any(item => item.Span.Contains(span));
        }

        private static void AddExistingBasicFields(
            DocumentReadResult document,
            ICollection<ProtectedTerm> target)
        {
            IReadOnlyList<BasicFieldOccurrence> occurrences =
                new BasicFieldExtractor().Extract(document);
            foreach (BasicFieldOccurrence occurrence in occurrences.Where(item =>
                item.FieldType == BasicFieldType.ProjectName ||
                item.FieldType == BasicFieldType.ReportNumber))
            {
                foreach (DocumentElement element in document.Elements.Where(item =>
                    SameLocation(item.Location, occurrence.Location)))
                {
                    ProtectLiteral(
                        target,
                        element,
                        occurrence.RawValue,
                        occurrence.FieldType.ToString());
                }
            }
        }

        private static void ProtectLiteral(
            ICollection<ProtectedTerm> target,
            DocumentElement element,
            string value,
            string kind)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            int start = 0;
            while ((start = element.Text.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
            {
                target.Add(new ProtectedTerm(
                    element.DocumentOrderIndex,
                    new TextSpan(start, value.Length),
                    kind,
                    value));
                start += value.Length;
            }
        }

        private static void AddMatches(
            ICollection<ProtectedTerm> target,
            DocumentElement element,
            string text,
            string kind,
            MatchCollection matches,
            string groupName = null)
        {
            foreach (Match match in matches)
            {
                Group group = string.IsNullOrEmpty(groupName) ? null : match.Groups[groupName];
                int start = group?.Success == true ? group.Index : match.Index;
                int length = group?.Success == true ? group.Length : match.Length;
                string original = length == 0 ? string.Empty : text.Substring(start, length);
                if (length > 0)
                {
                    target.Add(new ProtectedTerm(
                        element.DocumentOrderIndex,
                        new TextSpan(start, length),
                        kind,
                        original));
                }
            }
        }

        private static IEnumerable<ProtectedTerm> Deduplicate(IEnumerable<ProtectedTerm> terms)
        {
            return terms.GroupBy(item => string.Concat(
                    item.DocumentOrderIndex, "|", item.Span.Start, "|", item.Span.Length, "|", item.Kind))
                .Select(group => group.First());
        }

        private static bool SameLocation(DocumentLocation left, DocumentLocation right)
        {
            return left.DocumentPart == right.DocumentPart &&
                left.ParagraphIndex == right.ParagraphIndex &&
                left.TableIndex == right.TableIndex &&
                left.RowIndex == right.RowIndex &&
                left.CellIndex == right.CellIndex;
        }

        private sealed class ProtectionPattern
        {
            public ProtectionPattern(string kind, string pattern)
            {
                Kind = kind;
                Regex = new Regex(
                    pattern,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            }

            public string Kind { get; }
            public Regex Regex { get; }
        }
    }
}
