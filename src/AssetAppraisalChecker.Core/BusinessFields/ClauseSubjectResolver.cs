using System;

namespace AssetAppraisalChecker.Core.BusinessFields
{
    internal static class ClauseSubjectResolver
    {
        private static readonly string[] AssetMarkers =
        {
            "设备", "资产", "车辆", "房产", "房屋", "建筑物", "土地",
            "机器", "生产线", "存货", "构筑物", "装置", "标的"
        };

        private static readonly string[] GenericAssetNames =
        {
            "设备", "机器设备", "资产", "单项资产", "车辆", "房屋建筑物",
            "土地", "评估对象", "标的"
        };

        private static readonly string[] NonDistinguishingSubjectPhrases =
        {
            // Long phrases must be removed before their component words.
            "纳入本次评估范围", "纳入评估范围", "全部评估对象",
            "评估对象", "评估资产", "资产评估", "委估资产",
            "房屋建筑物", "机器设备", "单项资产",
            "生产线", "构筑物", "建筑物", "设备", "资产", "车辆",
            "房产", "房屋", "土地", "机器", "存货", "装置", "标的",
            "本次", "上述", "全部", "整体", "评估", "对象", "范围", "价值",
            "其中", "所涉及", "涉及", "纳入", "的"
        };

        private static readonly string[] LeadingPhrases =
        {
            "经评估", "经测算", "经计算", "其中", "上述"
        };

        private static readonly string[] TrailingQualifiers =
        {
            "不含增值税", "含增值税", "不含税", "含税", "人民币", "的"
        };

        public static bool TryResolve(string localContext, out SubjectResolution resolution)
        {
            ValueCategory category;
            string fieldKeyword;
            if (!BusinessKeywordMatcher.TryClassifyValue(localContext, out category, out fieldKeyword))
            {
                resolution = null;
                return false;
            }

            int fieldIndex = localContext.IndexOf(fieldKeyword, StringComparison.Ordinal);
            if (fieldIndex <= 0)
            {
                resolution = null;
                return false;
            }

            string candidate = SubjectTextNormalizer.Normalize(localContext.Substring(0, fieldIndex));
            candidate = TrimBoundaryPunctuation(candidate);
            candidate = RemoveLeadingPhrases(candidate);
            candidate = RemoveTrailingQualifiers(candidate);
            candidate = TrimBoundaryPunctuation(SubjectTextNormalizer.Normalize(candidate));

            if (!IsReliableAssetSubject(candidate))
            {
                resolution = null;
                return false;
            }

            resolution = new SubjectResolution(
                new ComparisonSubjectKey(SubjectKind.Asset, candidate),
                ClassificationConfidence.High,
                new[] { string.Format("当前金额所在子句明确包含资产主体“{0}”。", candidate) });
            return true;
        }

        private static string RemoveLeadingPhrases(string text)
        {
            string result = text;
            bool removed;
            do
            {
                removed = false;
                foreach (string phrase in LeadingPhrases)
                {
                    if (result.StartsWith(phrase, StringComparison.Ordinal))
                    {
                        result = TrimBoundaryPunctuation(result.Substring(phrase.Length));
                        removed = true;
                        break;
                    }
                }
            }
            while (removed);

            return result;
        }

        private static string RemoveTrailingQualifiers(string text)
        {
            string result = text;
            bool removed;
            do
            {
                removed = false;
                foreach (string qualifier in TrailingQualifiers)
                {
                    if (result.EndsWith(qualifier, StringComparison.Ordinal))
                    {
                        result = TrimBoundaryPunctuation(
                            result.Substring(0, result.Length - qualifier.Length));
                        removed = true;
                        break;
                    }
                }
            }
            while (removed);

            return result;
        }

        private static bool IsReliableAssetSubject(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 80)
            {
                return false;
            }

            foreach (string genericName in GenericAssetNames)
            {
                if (string.Equals(candidate, genericName, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            foreach (string marker in AssetMarkers)
            {
                if (candidate.IndexOf(marker, StringComparison.Ordinal) >= 0)
                {
                    return HasDistinguishingSubjectText(candidate);
                }
            }

            return false;
        }

        private static bool HasDistinguishingSubjectText(string candidate)
        {
            string remainder = candidate;
            foreach (string phrase in NonDistinguishingSubjectPhrases)
            {
                remainder = remainder.Replace(phrase, string.Empty);
            }

            remainder = TrimBoundaryPunctuation(
                SubjectTextNormalizer.Normalize(remainder));
            foreach (char character in remainder)
            {
                if (char.IsLetterOrDigit(character))
                {
                    return true;
                }
            }

            return false;
        }

        private static string TrimBoundaryPunctuation(string text)
        {
            return (text ?? string.Empty).Trim(' ', '\t', '\r', '\n', '，', ',', '；', ';', '。', '.', '：', ':');
        }
    }
}
