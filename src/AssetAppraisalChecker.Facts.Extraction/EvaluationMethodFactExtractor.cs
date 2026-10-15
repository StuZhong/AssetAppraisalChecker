using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.Facts.Extraction
{
    public sealed class EvaluationMethodFactExtractor
    {
        private static readonly SubjectDefinition[] Subjects =
        {
            new SubjectDefinition("房屋建筑物", EvaluationMethodSubjectKind.Building, "subject/v1:building"),
            new SubjectDefinition("机器设备", EvaluationMethodSubjectKind.MachineryEquipment, "subject/v1:machinery-equipment"),
            new SubjectDefinition("土地使用权", EvaluationMethodSubjectKind.LandUseRight, "subject/v1:land-use-right"),
            new SubjectDefinition("整体资产组", EvaluationMethodSubjectKind.AssetGroup, "subject/v1:asset-group"),
            new SubjectDefinition("企业整体", EvaluationMethodSubjectKind.EnterpriseWhole, "subject/v1:enterprise-whole"),
            new SubjectDefinition("单项资产", EvaluationMethodSubjectKind.SingleAsset, "subject/v1:single-asset")
        };

        private static readonly MethodDefinition[] Methods =
        {
            new MethodDefinition("资产基础法", EvaluationMethodCode.AssetBasedApproach, EvaluationMethodFamily.AssetBased, EvaluationMethodSpecificity.BasicApproach),
            new MethodDefinition("重置成本法", EvaluationMethodCode.ReplacementCostMethod, EvaluationMethodFamily.Cost, EvaluationMethodSpecificity.SpecificMethod),
            new MethodDefinition("成本法", EvaluationMethodCode.CostApproach, EvaluationMethodFamily.Cost, EvaluationMethodSpecificity.BasicApproach),
            new MethodDefinition("市场法", EvaluationMethodCode.MarketApproach, EvaluationMethodFamily.Market, EvaluationMethodSpecificity.BasicApproach),
            new MethodDefinition("收益法", EvaluationMethodCode.IncomeApproach, EvaluationMethodFamily.Income, EvaluationMethodSpecificity.BasicApproach)
        };

        private static readonly Regex FinalAdoption = new Regex(
            "最终(?:采用|选用|选取|选择)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public IReadOnlyList<EvaluationMethodFact> Extract(
            DocumentReadResult documentReadResult,
            string sourceDocument)
        {
            return ExtractWithDiagnostics(documentReadResult, sourceDocument).Facts;
        }

        public EvaluationMethodExtractionResult ExtractWithDiagnostics(
            DocumentReadResult documentReadResult,
            string sourceDocument)
        {
            if (documentReadResult == null)
            {
                throw new ArgumentNullException(nameof(documentReadResult));
            }

            if (string.IsNullOrWhiteSpace(sourceDocument))
            {
                throw new ArgumentException("事实来源文档不能为空。", nameof(sourceDocument));
            }

            var facts = new List<EvaluationMethodFact>();
            var statements = new List<EvaluationMethodStatement>();
            foreach (DocumentElement element in documentReadResult.Elements.OrderBy(item => item.DocumentOrderIndex))
            {
                ExtractElement(element, sourceDocument, facts, statements);
            }

            return new EvaluationMethodExtractionResult(facts, statements);
        }

        private static void ExtractElement(
            DocumentElement element,
            string sourceDocument,
            ICollection<EvaluationMethodFact> facts,
            ICollection<EvaluationMethodStatement> statements)
        {
            string text = element.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            List<SubjectOccurrence> subjectOccurrences = FindSubjects(text);
            if (HasCompetingSubjectBinding(text, subjectOccurrences))
            {
                AddAmbiguousSubjectStatement(element, sourceDocument, text, statements);
                return;
            }

            if (subjectOccurrences.Count == 0)
            {
                ExtractSubjectBlock(
                    element,
                    sourceDocument,
                    text,
                    new SubjectDefinition("整体评估对象", EvaluationMethodSubjectKind.GlobalAssessmentObject, "subject/v1:global"),
                    0,
                    facts,
                    statements);
                return;
            }

            for (int index = 0; index < subjectOccurrences.Count; index++)
            {
                SubjectOccurrence occurrence = subjectOccurrences[index];
                int end = index + 1 < subjectOccurrences.Count
                    ? subjectOccurrences[index + 1].Index
                    : text.Length;
                string block = text.Substring(occurrence.Index, end - occurrence.Index);
                ExtractSubjectBlock(
                    element,
                    sourceDocument,
                    block,
                    occurrence.Definition,
                    occurrence.Index,
                    facts,
                    statements);
            }
        }

        private static void ExtractSubjectBlock(
            DocumentElement element,
            string sourceDocument,
            string block,
            SubjectDefinition subject,
            int blockOffset,
            ICollection<EvaluationMethodFact> facts,
            ICollection<EvaluationMethodStatement> statements)
        {
            List<MethodOccurrence> occurrences = FindMethods(block);
            if (occurrences.Count == 0)
            {
                if (ContainsNonStandardReplacementDescription(block))
                {
                    statements.Add(CreateStatement(
                        subject,
                        EvaluationMethodCode.Unknown,
                        EvaluationMethodApplicability.CandidateUnknown,
                        sourceDocument,
                        element,
                        block.Trim(),
                        0.45m,
                        "非标准方法描述未被确定性规则归一。"));
                }

                return;
            }

            var adopted = new List<MethodOccurrence>();
            foreach (MethodOccurrence occurrence in occurrences)
            {
                EvaluationMethodApplicability applicability = DetermineApplicability(block, occurrence.Index);
                string prefix = ClausePrefix(block, occurrence.Index);
                occurrence.Role = DetermineRole(block, occurrence);
                // A final method is not a document-global fact merely because no subject
                // was found. Do not inherit a subject across an unrelated sentence.
                bool uncertainFinal = occurrence.Role == EvaluationMethodRole.FinalAdopted &&
                    (ContainsAny(prefix, "未", "不", "非", "可能", "拟", "建议", "如果", "若") ||
                     ContainsAny(ClauseText(block, occurrence), "或", "待定", "尚未确定") ||
                     (subject.Kind == EvaluationMethodSubjectKind.GlobalAssessmentObject &&
                      !HasLocalGlobalSubject(block, occurrence.Index)) ||
                     ContainsAny(prefix, "含税", "净值", "原值", "账面", "投资价值", "清算价值"));
                if (uncertainFinal)
                {
                    applicability = EvaluationMethodApplicability.MentionedOnly;
                }
                string original = occurrence.Role == EvaluationMethodRole.Unspecified
                    ? ExtractMethodEvidence(block, occurrence)
                    : prefix + occurrence.Definition.DisplayName;
                EvaluationMethodStatement statement = CreateStatement(
                    subject,
                    occurrence.Definition.Code,
                    applicability,
                    sourceDocument,
                    element,
                    original,
                    uncertainFinal ? 0.45m : applicability == EvaluationMethodApplicability.Adopted ? 0.95m : 0.90m,
                    CreateApplicabilityEvidence(applicability));
                statements.Add(statement);
                if (applicability == EvaluationMethodApplicability.Adopted)
                {
                    adopted.Add(occurrence);
                }
            }

            EvaluationMethodSetCompleteness completeness = DetermineCompleteness(block, adopted.Count);
            string methodSetId = string.Concat(
                "method-set/v1:",
                sourceDocument,
                ":",
                element.DocumentOrderIndex,
                ":",
                blockOffset);
            foreach (MethodOccurrence occurrence in adopted)
            {
                MethodDefinition method = occurrence.Definition;
                string original = occurrence.Role == EvaluationMethodRole.Unspecified
                    ? ExtractMethodEvidence(block, occurrence)
                    : ClausePrefix(block, occurrence.Index) + method.DisplayName;
                facts.Add(new EvaluationMethodFact(
                    method.DisplayName,
                    method.Code,
                    method.Family,
                    method.Specificity,
                    subject.DisplayName,
                    subject.Kind,
                    subject.Key,
                    occurrence.Role,
                    occurrence.Role == EvaluationMethodRole.Unspecified ? methodSetId :
                        methodSetId + ":" + ClauseStart(block, occurrence.Index) + ":" + occurrence.Role,
                    occurrence.Role == EvaluationMethodRole.FinalAdopted
                        ? EvaluationMethodSetCompleteness.Complete : completeness,
                    sourceDocument,
                    element.Location,
                    original,
                    new FactConfidence(FactConfidenceBand.High, 0.95m),
                    new[]
                    {
                        string.Concat(subject.DisplayName, " | ", original),
                        "Applicability=Adopted"
                    }.Concat(occurrence.Role == EvaluationMethodRole.Unspecified
                        ? new string[0] : new[] { "MethodRole=" + occurrence.Role })));
            }
        }

        private static List<SubjectOccurrence> FindSubjects(string text)
        {
            var result = new List<SubjectOccurrence>();
            foreach (SubjectDefinition subject in Subjects)
            {
                int start = 0;
                while (start < text.Length)
                {
                    int index = text.IndexOf(subject.DisplayName, start, StringComparison.Ordinal);
                    if (index < 0)
                    {
                        break;
                    }

                    result.Add(new SubjectOccurrence(index, subject));
                    start = index + subject.DisplayName.Length;
                }
            }

            return result.OrderBy(item => item.Index).ToList();
        }

        private static bool HasCompetingSubjectBinding(
            string text,
            IReadOnlyList<SubjectOccurrence> subjects)
        {
            if (subjects.Count < 2)
            {
                return false;
            }

            for (int index = 0; index < subjects.Count - 1; index++)
            {
                int betweenStart = subjects[index].Index + subjects[index].Definition.DisplayName.Length;
                int betweenLength = subjects[index + 1].Index - betweenStart;
                string between = text.Substring(betweenStart, betweenLength);
                if ((between.Contains("和") || between.Contains("及") || between.Contains("、"))
                    && !ContainsSentenceBoundary(between))
                {
                    int methodIndex = FindFirstMethodIndex(text, subjects[index].Index);
                    if (methodIndex > subjects[index + 1].Index)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddAmbiguousSubjectStatement(
            DocumentElement element,
            string sourceDocument,
            string text,
            ICollection<EvaluationMethodStatement> statements)
        {
            statements.Add(new EvaluationMethodStatement(
                "未解析对象",
                EvaluationMethodSubjectKind.Unspecified,
                "subject/v1:unresolved",
                EvaluationMethodCode.Unknown,
                EvaluationMethodApplicability.CandidateUnknown,
                sourceDocument,
                element.Location,
                text.Trim(),
                new FactConfidence(FactConfidenceBand.Low, 0.20m),
                new[] { "多个 Subject 候选共享方法表达，规则无法可靠绑定。" }));
        }

        private static List<MethodOccurrence> FindMethods(string text)
        {
            var matches = new List<MethodOccurrence>();
            var occupied = new List<Tuple<int, int>>();
            foreach (MethodDefinition method in Methods)
            {
                int start = 0;
                while (start < text.Length)
                {
                    int index = text.IndexOf(method.DisplayName, start, StringComparison.Ordinal);
                    if (index < 0)
                    {
                        break;
                    }

                    int end = index + method.DisplayName.Length;
                    if (!occupied.Any(span => index >= span.Item1 && end <= span.Item2))
                    {
                        matches.Add(new MethodOccurrence(index, method));
                        occupied.Add(Tuple.Create(index, end));
                    }

                    start = end;
                }
            }

            return matches.OrderBy(item => item.Index).ToList();
        }

        private static EvaluationMethodApplicability DetermineApplicability(string block, int methodIndex)
        {
            string prefix = ClausePrefix(block, methodIndex);
            if (ContainsAny(prefix, "不适用", "并不适用"))
            {
                return EvaluationMethodApplicability.NotApplicable;
            }

            if (ContainsAny(prefix, "不采用", "未采用", "没有采用", "不选用", "未选用", "不宜采用", "无法采用"))
            {
                return EvaluationMethodApplicability.Rejected;
            }

            if (ContainsAny(prefix, "采用", "选用", "使用") || FinalAdoption.IsMatch(prefix))
            {
                return EvaluationMethodApplicability.Adopted;
            }

            return EvaluationMethodApplicability.MentionedOnly;
        }

        private static EvaluationMethodRole DetermineRole(string block, MethodOccurrence occurrence)
        {
            string prefix = ClausePrefix(block, occurrence.Index);
            if (FinalAdoption.IsMatch(prefix)) return EvaluationMethodRole.FinalAdopted;
            if (ContainsAny(ClauseText(block, occurrence),
                "测算", "计算")) return EvaluationMethodRole.MethodCalculation;
            return EvaluationMethodRole.Unspecified;
        }

        private static string ClauseText(string block, MethodOccurrence occurrence)
        {
            int start = ClauseStart(block, occurrence.Index);
            int end = occurrence.Index + occurrence.Definition.DisplayName.Length;
            while (end < block.Length && "，,；;。！？!?\r\n".IndexOf(block[end]) < 0) end++;
            return block.Substring(start, end - start);
        }

        private static bool HasLocalGlobalSubject(string block, int methodIndex)
        {
            int start = methodIndex;
            while (start > 0 && "；;。！？!?\r\n".IndexOf(block[start - 1]) < 0) start--;
            return ContainsAny(block.Substring(start, methodIndex - start),
                "本次评估", "本项目", "本次项目", "评估对象整体");
        }

        private static int ClauseStart(string block, int index)
        {
            while (index > 0 && "，,；;。！？!?\r\n".IndexOf(block[index - 1]) < 0) index--;
            return index;
        }

        private static string ClausePrefix(string block, int methodIndex)
        {
            int start = ClauseStart(block, methodIndex);
            return block.Substring(start, methodIndex - start);
        }

        private static EvaluationMethodSetCompleteness DetermineCompleteness(string block, int adoptedCount)
        {
            if (ContainsAny(block, "仅采用", "只采用", "最终采用", "确定采用") || adoptedCount > 1)
            {
                return EvaluationMethodSetCompleteness.Complete;
            }

            return EvaluationMethodSetCompleteness.Unknown;
        }

        private static string ExtractMethodEvidence(string block, MethodOccurrence occurrence)
        {
            int prefixStart = Math.Max(0, occurrence.Index - 4);
            string candidate = block.Substring(
                prefixStart,
                occurrence.Index - prefixStart + occurrence.Definition.DisplayName.Length);
            foreach (string phrase in new[] { "不适用", "不采用", "未采用", "不选用", "未选用", "采用", "选用", "使用" })
            {
                int index = candidate.LastIndexOf(phrase, StringComparison.Ordinal);
                if (index >= 0)
                {
                    return candidate.Substring(index);
                }
            }

            return occurrence.Definition.DisplayName;
        }

        private static EvaluationMethodStatement CreateStatement(
            SubjectDefinition subject,
            EvaluationMethodCode methodCode,
            EvaluationMethodApplicability applicability,
            string sourceDocument,
            DocumentElement element,
            string originalText,
            decimal confidence,
            string evidence)
        {
            return new EvaluationMethodStatement(
                subject.DisplayName,
                subject.Kind,
                subject.Key,
                methodCode,
                applicability,
                sourceDocument,
                element.Location,
                originalText,
                new FactConfidence(
                    confidence >= 0.80m ? FactConfidenceBand.High : FactConfidenceBand.Low,
                    confidence),
                new[] { evidence });
        }

        private static bool ContainsNonStandardReplacementDescription(string text)
        {
            return ContainsAny(text, "现行重建成本", "重建成本测算", "重置成本途径");
        }

        private static string CreateApplicabilityEvidence(EvaluationMethodApplicability applicability)
        {
            return "Applicability=" + applicability;
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            return values.Any(value => text.IndexOf(value, StringComparison.Ordinal) >= 0);
        }

        private static bool ContainsSentenceBoundary(string text)
        {
            return text.IndexOf('；') >= 0 || text.IndexOf(';') >= 0 || text.IndexOf('。') >= 0;
        }

        private static int FindFirstMethodIndex(string text, int start)
        {
            int result = -1;
            foreach (MethodDefinition method in Methods)
            {
                int index = text.IndexOf(method.DisplayName, start, StringComparison.Ordinal);
                if (index >= 0 && (result < 0 || index < result))
                {
                    result = index;
                }
            }

            return result;
        }

        private sealed class SubjectDefinition
        {
            public SubjectDefinition(string displayName, EvaluationMethodSubjectKind kind, string key)
            {
                DisplayName = displayName;
                Kind = kind;
                Key = key;
            }

            public string DisplayName { get; }
            public EvaluationMethodSubjectKind Kind { get; }
            public string Key { get; }
        }

        private sealed class SubjectOccurrence
        {
            public SubjectOccurrence(int index, SubjectDefinition definition)
            {
                Index = index;
                Definition = definition;
            }

            public int Index { get; }
            public SubjectDefinition Definition { get; }
        }

        private sealed class MethodDefinition
        {
            public MethodDefinition(
                string displayName,
                EvaluationMethodCode code,
                EvaluationMethodFamily family,
                EvaluationMethodSpecificity specificity)
            {
                DisplayName = displayName;
                Code = code;
                Family = family;
                Specificity = specificity;
            }

            public string DisplayName { get; }
            public EvaluationMethodCode Code { get; }
            public EvaluationMethodFamily Family { get; }
            public EvaluationMethodSpecificity Specificity { get; }
        }

        private sealed class MethodOccurrence
        {
            public MethodOccurrence(int index, MethodDefinition definition)
            {
                Index = index;
                Definition = definition;
            }

            public int Index { get; }
            public MethodDefinition Definition { get; }
            public EvaluationMethodRole Role { get; set; }
        }
    }
}
