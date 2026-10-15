using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace AssetAppraisalChecker.Mechanical.Formatting
{
    internal sealed class StyleCatalog
    {
        private readonly IReadOnlyDictionary<string, Style> styles;

        public StyleCatalog(Styles source)
        {
            List<Style> values = source == null
                ? new List<Style>()
                : source.Elements<Style>()
                    .Where(item => item.StyleId != null && !string.IsNullOrWhiteSpace(item.StyleId.Value))
                    .ToList();
            styles = new ReadOnlyDictionary<string, Style>(
                values.GroupBy(item => item.StyleId.Value, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal));
            Metadata = new ReadOnlyCollection<StyleMetadataSnapshot>(values
                .Select(item => new StyleMetadataSnapshot(
                    item.StyleId?.Value,
                    item.Type?.InnerText,
                    item.BasedOn?.Val?.Value,
                    item.StyleName?.Val?.Value,
                    IsEnabled(item.Default)))
                .ToList());
        }

        public IReadOnlyList<StyleMetadataSnapshot> Metadata { get; }

        public StyleChainResolution Resolve(string styleId, StyleValues expectedType)
        {
            if (string.IsNullOrWhiteSpace(styleId))
            {
                Style defaultStyle = styles.Values.FirstOrDefault(item =>
                    (item.Type == null || item.Type.Value == expectedType) &&
                    IsEnabled(item.Default));
                if (defaultStyle == null)
                {
                    return StyleChainResolution.Resolved(new Style[0], string.Empty);
                }

                styleId = defaultStyle.StyleId.Value;
            }

            var chain = new List<Style>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string currentId = styleId;
            while (!string.IsNullOrWhiteSpace(currentId))
            {
                if (!visited.Add(currentId))
                {
                    return StyleChainResolution.Unresolved(
                        "BasedOn cycle: " + string.Join(" -> ", visited) + " -> " + currentId);
                }

                Style current;
                if (!styles.TryGetValue(currentId, out current))
                {
                    return StyleChainResolution.Unresolved("Missing style: " + currentId);
                }

                if (current.Type != null && current.Type.Value != expectedType)
                {
                    return StyleChainResolution.Unresolved(
                        "Style type mismatch: " + currentId + " (" + current.Type.Value + ")");
                }

                chain.Add(current);
                currentId = current.BasedOn?.Val?.Value;
            }

            chain.Reverse();
            return StyleChainResolution.Resolved(chain, styleId);
        }

        private static bool IsEnabled(OnOffValue value)
        {
            return value != null && value.Value;
        }
    }

    internal sealed class StyleChainResolution
    {
        private StyleChainResolution(
            FormattingResolutionStatus status,
            IEnumerable<Style> styles,
            string appliedStyleId,
            string message)
        {
            Status = status;
            Styles = new ReadOnlyCollection<Style>((styles ?? Enumerable.Empty<Style>()).ToList());
            AppliedStyleId = appliedStyleId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public FormattingResolutionStatus Status { get; }
        public IReadOnlyList<Style> Styles { get; }
        public string AppliedStyleId { get; }
        public string Message { get; }

        public static StyleChainResolution Resolved(IEnumerable<Style> styles, string appliedStyleId)
        {
            return new StyleChainResolution(
                FormattingResolutionStatus.Resolved,
                styles,
                appliedStyleId,
                null);
        }

        public static StyleChainResolution Unresolved(string message)
        {
            return new StyleChainResolution(
                FormattingResolutionStatus.Unresolved,
                Enumerable.Empty<Style>(),
                string.Empty,
                message);
        }
    }
}
