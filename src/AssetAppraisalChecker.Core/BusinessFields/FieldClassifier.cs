using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal sealed class FieldClassifier
    {
        public FieldClassification Classify(
            DocumentElement element,
            string localContext,
            TableBusinessContext tableContext)
        {
            var evidence = new List<string>();
            List<ClassificationSource> sources = BuildSources(element, localContext, tableContext);

            string sectionHeading;
            SectionCategory sectionCategory = SectionClassifier.Classify(element.HeadingPath, out sectionHeading);

            ClassificationConfidence valueConfidence;
            ValueCategory valueCategory = ResolveValueCategory(
                sources,
                tableContext,
                evidence,
                out valueConfidence);

            ClassificationConfidence taxConfidence;
            TaxBasis taxBasis = ResolveTaxBasis(
                sources,
                tableContext,
                evidence,
                out taxConfidence);

            ClassificationConfidence scopeConfidence;
            Scope scope = ResolveScope(
                BuildScopeSources(element, localContext, tableContext),
                localContext,
                tableContext,
                sectionCategory,
                evidence,
                out scopeConfidence);

            if (sectionCategory != SectionCategory.Other)
            {
                evidence.Add(string.Format(
                    "标题路径“{0}”识别为文档区域 {1}。",
                    sectionHeading,
                    sectionCategory));
            }

            if (valueCategory == ValueCategory.Unknown)
            {
                evidence.Add("未找到可靠的金额业务字段关键词，保留为 Unknown。");
            }

            var identity = new FieldIdentity(valueCategory, taxBasis, scope, sectionCategory);
            ClassificationConfidence confidence = CalculateOverallConfidence(
                valueCategory,
                valueConfidence,
                taxBasis,
                taxConfidence,
                scope,
                scopeConfidence,
                sectionCategory);

            return new FieldClassification(
                identity,
                confidence,
                valueConfidence,
                taxConfidence,
                scopeConfidence,
                new ReadOnlyCollection<string>(evidence));
        }

        private static List<ClassificationSource> BuildSources(
            DocumentElement element,
            string localContext,
            TableBusinessContext tableContext)
        {
            var sources = new List<ClassificationSource>();
            AddSource(sources, localContext, "当前金额所在子句", ClassificationConfidence.High, false);
            AddSource(
                sources,
                tableContext.ColumnHeaderText,
                "当前表格列标题",
                ClassificationConfidence.High,
                false);
            AddSource(
                sources,
                tableContext.RowLabelText,
                "当前表格行标签",
                ClassificationConfidence.High,
                false);

            foreach (string heading in element.HeadingPath.Reverse())
            {
                AddSource(sources, heading, "标题路径", ClassificationConfidence.Medium, true);
            }

            AddSource(
                sources,
                element.NearbyContext.PreviousText,
                "前一有效元素",
                ClassificationConfidence.Low,
                false);
            AddSource(
                sources,
                element.NearbyContext.NextText,
                "后一有效元素",
                ClassificationConfidence.Low,
                false);
            return sources;
        }

        private static List<ClassificationSource> BuildScopeSources(
            DocumentElement element,
            string localContext,
            TableBusinessContext tableContext)
        {
            var sources = new List<ClassificationSource>();
            AddSource(sources, localContext, "当前金额所在子句", ClassificationConfidence.High, false);
            AddSource(
                sources,
                tableContext.RowLabelText,
                "当前表格行标签",
                ClassificationConfidence.High,
                false);
            AddSource(
                sources,
                tableContext.ColumnHeaderText,
                "当前表格列标题",
                ClassificationConfidence.High,
                false);

            foreach (string heading in element.HeadingPath.Reverse())
            {
                AddSource(sources, heading, "标题路径", ClassificationConfidence.Medium, true);
            }

            AddSource(
                sources,
                element.NearbyContext.PreviousText,
                "前一有效元素",
                ClassificationConfidence.Low,
                false);
            return sources;
        }

        private static void AddSource(
            ICollection<ClassificationSource> sources,
            string text,
            string description,
            ClassificationConfidence confidence,
            bool isHeading)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                sources.Add(new ClassificationSource(text, description, confidence, isHeading));
            }
        }

        private static ValueCategory ResolveValueCategory(
            IEnumerable<ClassificationSource> sources,
            TableBusinessContext tableContext,
            ICollection<string> evidence,
            out ClassificationConfidence confidence)
        {
            if (tableContext.MergedHeaderSemantic != null)
            {
                evidence.Add(string.Format(
                    "当前表格逻辑网格列由唯一父子表头组合识别为 {0}。",
                    tableContext.MergedHeaderSemantic.ValueCategory));
                confidence = ClassificationConfidence.High;
                return tableContext.MergedHeaderSemantic.ValueCategory;
            }

            foreach (ClassificationSource source in sources)
            {
                ValueCategory category;
                string keyword;
                if (BusinessKeywordMatcher.TryClassifyValue(source.Text, out category, out keyword))
                {
                    evidence.Add(CreateEvidence(source, keyword, category.ToString()));
                    confidence = source.Confidence;
                    return category;
                }
            }

            confidence = ClassificationConfidence.Low;
            return ValueCategory.Unknown;
        }

        private static TaxBasis ResolveTaxBasis(
            IEnumerable<ClassificationSource> sources,
            TableBusinessContext tableContext,
            ICollection<string> evidence,
            out ClassificationConfidence confidence)
        {
            if (tableContext.MergedHeaderSemantic != null)
            {
                evidence.Add(string.Format(
                    "当前表格逻辑网格列的唯一父表头识别为 {0}。",
                    tableContext.MergedHeaderSemantic.TaxBasis));
                confidence = ClassificationConfidence.High;
                return tableContext.MergedHeaderSemantic.TaxBasis;
            }

            foreach (ClassificationSource source in sources)
            {
                TaxBasis taxBasis;
                string keyword;
                if (BusinessKeywordMatcher.TryClassifyTaxBasis(source.Text, out taxBasis, out keyword))
                {
                    evidence.Add(CreateEvidence(source, keyword, taxBasis.ToString()));
                    confidence = source.Confidence;
                    return taxBasis;
                }
            }

            confidence = ClassificationConfidence.High;
            return TaxBasis.Unspecified;
        }

        private static Scope ResolveScope(
            IList<ClassificationSource> sources,
            string localContext,
            TableBusinessContext tableContext,
            SectionCategory sectionCategory,
            ICollection<string> evidence,
            out ClassificationConfidence confidence)
        {
            // A calculation-case heading defines the containing scope and must not be replaced
            // by a final-value sounding phrase inside the worked example.
            foreach (ClassificationSource source in sources.Where(item => item.IsHeading))
            {
                Scope headingScope;
                string headingKeyword;
                if (BusinessKeywordMatcher.TryClassifyScope(source.Text, out headingScope, out headingKeyword) &&
                    headingScope == Scope.CalculationCase)
                {
                    evidence.Add(CreateEvidence(source, headingKeyword, headingScope.ToString()));
                    confidence = ClassificationConfidence.High;
                    return headingScope;
                }
            }

            string aggregateCue;
            if (ProjectAggregateContextMatcher.TryMatch(
                localContext,
                sectionCategory,
                out aggregateCue))
            {
                evidence.Add(string.Format(
                    "当前金额所在子句包含项目汇总证据“{0}”，且位于明确的{1}区域，识别为 ProjectTotal。",
                    aggregateCue,
                    sectionCategory));
                confidence = ClassificationConfidence.High;
                return Scope.ProjectTotal;
            }

            if (tableContext.IsMergedHeaderProjectAggregateRow)
            {
                evidence.Add(
                    "当前双层合并金额表的同一行包含唯一项目聚合标签，识别为 ProjectTotal。");
                confidence = ClassificationConfidence.High;
                return Scope.ProjectTotal;
            }

            foreach (ClassificationSource source in sources)
            {
                Scope scope;
                string keyword;
                if (BusinessKeywordMatcher.TryClassifyScope(source.Text, out scope, out keyword))
                {
                    evidence.Add(CreateEvidence(source, keyword, scope.ToString()));
                    confidence = source.Confidence;
                    return scope;
                }
            }

            confidence = ClassificationConfidence.High;
            return Scope.Unspecified;
        }

        private static string CreateEvidence(
            ClassificationSource source,
            string keyword,
            string classification)
        {
            return string.Format(
                "{0}包含“{1}”，识别为 {2}。",
                source.Description,
                keyword,
                classification);
        }

        private static ClassificationConfidence CalculateOverallConfidence(
            ValueCategory valueCategory,
            ClassificationConfidence valueConfidence,
            TaxBasis taxBasis,
            ClassificationConfidence taxConfidence,
            Scope scope,
            ClassificationConfidence scopeConfidence,
            SectionCategory sectionCategory)
        {
            if (valueCategory == ValueCategory.Unknown)
            {
                return ClassificationConfidence.Low;
            }

            int confidence = (int)valueConfidence;
            if (taxBasis != TaxBasis.Unspecified)
            {
                confidence = System.Math.Min(confidence, (int)taxConfidence);
            }

            if (scope != Scope.Unspecified)
            {
                confidence = System.Math.Min(confidence, (int)scopeConfidence);
            }

            if (sectionCategory != SectionCategory.Other)
            {
                confidence = System.Math.Min(confidence, (int)ClassificationConfidence.High);
            }

            return (ClassificationConfidence)confidence;
        }

        private sealed class ClassificationSource
        {
            public ClassificationSource(
                string text,
                string description,
                ClassificationConfidence confidence,
                bool isHeading)
            {
                Text = text;
                Description = description;
                Confidence = confidence;
                IsHeading = isHeading;
            }

            public string Text { get; }

            public string Description { get; }

            public ClassificationConfidence Confidence { get; }

            public bool IsHeading { get; }
        }
    }
}
