using System;

namespace AssetAppraisalChecker.Semantic.Tests.ModelAcceptance
{
    internal sealed class LocalModelAdapterProfile
    {
        private LocalModelAdapterProfile(
            string modelId,
            string queryPrefix,
            string passagePrefix,
            bool normalizeEmbeddings,
            string poolingStrategy,
            string adapterVersion)
        {
            ModelId = modelId;
            QueryPrefix = queryPrefix;
            PassagePrefix = passagePrefix;
            NormalizeEmbeddings = normalizeEmbeddings;
            PoolingStrategy = poolingStrategy;
            AdapterVersion = adapterVersion;
        }

        internal string ModelId { get; }
        internal string QueryPrefix { get; }
        internal string PassagePrefix { get; }
        internal bool NormalizeEmbeddings { get; }
        internal string PoolingStrategy { get; }
        internal string AdapterVersion { get; }

        internal string PrepareQuery(string text)
        {
            return QueryPrefix + RequireText(text);
        }

        internal string PreparePassage(string text)
        {
            return PassagePrefix + RequireText(text);
        }

        internal static LocalModelAdapterProfile BgeSmallZhV15()
        {
            return new LocalModelAdapterProfile(
                "BAAI/bge-small-zh-v1.5",
                "为这个句子生成表示以用于检索相关文章：",
                string.Empty,
                true,
                "CLS+L2",
                "bge-small-zh-v1.5-adapter/experimental-v1");
        }

        internal static LocalModelAdapterProfile MultilingualE5Small()
        {
            return new LocalModelAdapterProfile(
                "intfloat/multilingual-e5-small",
                "query: ",
                "passage: ",
                true,
                "MeanPooling+L2",
                "multilingual-e5-small-adapter/experimental-v1");
        }

        private static string RequireText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Adapter input text is required.", nameof(value));
            }

            return value;
        }
    }
}
