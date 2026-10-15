using System;
using System.Collections.Generic;
using System.Linq;
using AssetAppraisalChecker.CrossDocument;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using AssetAppraisalChecker.Semantic;

namespace AssetAppraisalChecker.Application
{
    public sealed class EvaluationMethodChecker : IFactChecker
    {
        public const string Name = "EvaluationMethodChecker";

        private readonly EvaluationMethodFactExtractor _extractor;
        private readonly EvaluationMethodComparer _comparer;
        private readonly ISemanticAnalyzer _semanticAnalyzer;
        private readonly ISemanticCandidateRanker _semanticCandidateRanker;
        private readonly SemanticCandidateProcessingOptions _semanticProcessingOptions;
        private readonly SemanticCandidateAcceptanceGate _semanticAcceptanceGate;

        public EvaluationMethodChecker(
            EvaluationMethodFactExtractor extractor,
            EvaluationMethodComparer comparer)
            : this(extractor, comparer, null, null)
        {
        }

        public EvaluationMethodChecker(
            EvaluationMethodFactExtractor extractor,
            EvaluationMethodComparer comparer,
            ISemanticAnalyzer semanticAnalyzer)
            : this(extractor, comparer, semanticAnalyzer, null)
        {
        }

        public EvaluationMethodChecker(
            EvaluationMethodFactExtractor extractor,
            EvaluationMethodComparer comparer,
            ISemanticAnalyzer semanticAnalyzer,
            ISemanticCandidateRanker semanticCandidateRanker)
            : this(
                extractor,
                comparer,
                semanticAnalyzer,
                semanticCandidateRanker,
                SemanticCandidateProcessingOptions.Shadow)
        {
        }

        public EvaluationMethodChecker(
            EvaluationMethodFactExtractor extractor,
            EvaluationMethodComparer comparer,
            ISemanticAnalyzer semanticAnalyzer,
            ISemanticCandidateRanker semanticCandidateRanker,
            SemanticCandidateProcessingOptions semanticProcessingOptions)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            _semanticAnalyzer = semanticAnalyzer;
            _semanticCandidateRanker = semanticCandidateRanker;
            _semanticProcessingOptions = semanticProcessingOptions
                ?? throw new ArgumentNullException(nameof(semanticProcessingOptions));
            _semanticAcceptanceGate = new SemanticCandidateAcceptanceGate();
        }

        public string CheckerName => Name;
        public FactType FactType => FactType.EvaluationMethod;

        public FactCheckExecutionEnvelope Execute(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            if (firstDocument == null)
            {
                throw new ArgumentNullException(nameof(firstDocument));
            }

            if (secondDocument == null)
            {
                throw new ArgumentNullException(nameof(secondDocument));
            }

            try
            {
                EvaluationMethodExtractionResult first = _extractor.ExtractWithDiagnostics(
                    firstDocument.DocumentReadResult,
                    firstDocument.SourceDocument);
                EvaluationMethodExtractionResult second = _extractor.ExtractWithDiagnostics(
                    secondDocument.DocumentReadResult,
                    secondDocument.SourceDocument);

                if (first.Facts.Count == 0
                    && second.Facts.Count == 0
                    && first.Statements.Count == 0
                    && second.Statements.Count == 0)
                {
                    return FactCheckExecutionEnvelope.NotApplicable(
                        CheckerName,
                        FactType,
                        "两个文档均无可归属的 EvaluationMethod 声明。兼容既有非方法类 Golden Case，不制造业务结论。");
                }

                IReadOnlyList<ConsistencyResult> results = _comparer.Compare(
                    first.Facts,
                    second.Facts,
                    first.Statements,
                    second.Statements);
                if (results.Count == 0)
                {
                    return FactCheckExecutionEnvelope.NotApplicable(
                        CheckerName,
                        FactType,
                        "没有形成可稳定标识的 EvaluationMethod Subject 结果。");
                }

                var executionEvidence = new List<string>();
                var activeResultEvidence = new Dictionary<string, List<string>>(
                    StringComparer.Ordinal);
                try
                {
                    AnalyzeUnresolvedCandidates(
                        first,
                        results,
                        executionEvidence,
                        activeResultEvidence);
                    AnalyzeUnresolvedCandidates(
                        second,
                        results,
                        executionEvidence,
                        activeResultEvidence);
                }
                catch (Exception exception)
                {
                    return FactCheckExecutionEnvelope.Failed(
                        CheckerName,
                        FactType,
                        results,
                        exception.Message,
                        executionEvidence);
                }

                IReadOnlyList<ConsistencyResult> finalResults =
                    AttachActiveSemanticEvidence(results, activeResultEvidence);

                return FactCheckExecutionEnvelope.FromResults(
                    CheckerName,
                    FactType,
                    finalResults,
                    executionEvidence);
            }
            catch (Exception exception)
            {
                return FactCheckExecutionEnvelope.Failed(
                    CheckerName,
                    FactType,
                    new ConsistencyResult[0],
                    exception.Message,
                    CreateFailureEvidence(firstDocument, secondDocument));
            }
        }

        private void AnalyzeUnresolvedCandidates(
            EvaluationMethodExtractionResult extraction,
            IReadOnlyList<ConsistencyResult> results,
            ICollection<string> executionEvidence,
            IDictionary<string, List<string>> activeResultEvidence)
        {
            if (_semanticAnalyzer == null && _semanticCandidateRanker == null)
            {
                return;
            }

            foreach (EvaluationMethodStatement statement in extraction.Statements)
            {
                if (statement.Applicability != EvaluationMethodApplicability.CandidateUnknown)
                {
                    continue;
                }

                if (_semanticAnalyzer != null
                    && (_semanticCandidateRanker == null
                        || _semanticProcessingOptions.ShadowOnly))
                {
                    string semanticInput = string.Concat(
                        "FieldName=EvaluationMethod\n",
                        "OriginalValue=",
                        statement.OriginalText,
                        "\nCandidateValue=UnresolvedMethodCandidate");
                    SemanticAnalysisResult semantic = _semanticAnalyzer.Analyze(semanticInput);
                    executionEvidence.Add(string.Concat(
                        statement.SourceDocument,
                        " | SemanticCandidateOnly | SimilarityScore=",
                        semantic.SimilarityScore,
                        " | Confidence=",
                        semantic.Confidence));
                }

                if (_semanticCandidateRanker != null)
                {
                    foreach (EvaluationMethodCandidateSafetyContext candidateContext in
                        EvaluationMethodCandidateSafetyContext.Resolve(statement.OriginalText))
                    {
                        AddRankingEvidence(
                            statement,
                            candidateContext,
                            results,
                            executionEvidence,
                            activeResultEvidence);
                    }
                }
            }
        }

        private void AddRankingEvidence(
            EvaluationMethodStatement statement,
            EvaluationMethodCandidateSafetyContext candidateContext,
            IReadOnlyList<ConsistencyResult> results,
            ICollection<string> executionEvidence,
            IDictionary<string, List<string>> activeResultEvidence)
        {
            try
            {
                SemanticCandidateResolution resolution = string.Equals(
                    statement.NormalizedSubjectKey,
                    SemanticCandidateAcceptanceGate.UnresolvedSubjectKey,
                    StringComparison.Ordinal) || candidateContext.IsUncertain
                    ? SemanticCandidateResolution.Ambiguous
                    : SemanticCandidateResolution.Resolvable;
                bool hardRuleConflict = results.Any(result =>
                    string.Equals(
                        result.Identity.SubjectKey,
                        statement.NormalizedSubjectKey,
                        StringComparison.Ordinal)
                    && result.Status == ConsistencyStatus.FAIL);
                if (hardRuleConflict)
                {
                    string protectedEvidence = string.Concat(
                        _semanticProcessingOptions.ShadowOnly
                            ? "SemanticShadow"
                            : "SemanticActive",
                        " | Status=Rejected | Reason=HardRuleConflict",
                        " | SubjectKey=",
                        statement.NormalizedSubjectKey,
                        " | FactPromotionAllowed=false");
                    executionEvidence.Add(protectedEvidence);
                    if (!_semanticProcessingOptions.ShadowOnly)
                    {
                        AddActiveResultEvidence(
                            statement.NormalizedSubjectKey,
                            new[] { protectedEvidence },
                            activeResultEvidence);
                    }

                    return;
                }

                SemanticCandidateRequest request = CreateRankingRequest(statement, candidateContext);
                executionEvidence.Add(string.Concat(
                    "SemanticCandidateContext | RequestId=", request.RequestId,
                    " | Polarity=", candidateContext.IsUncertain ? "Uncertain" : candidateContext.HasNegation ? "Negated" : "Affirmative",
                    " | FactPromotionAllowed=false"));
                SemanticCandidateRankingResult ranking = _semanticCandidateRanker.Rank(
                    request,
                    new SemanticCandidateSelectionContext(
                        resolution,
                        _semanticProcessingOptions.ShadowOnly));
                foreach (string evidence in ranking.ToSafeEvidence())
                {
                    executionEvidence.Add(evidence);
                }

                SemanticCandidateRankingAcceptanceDecision decision =
                    _semanticAcceptanceGate.EvaluateRanked(
                        ranking,
                        new SemanticCandidateRankingAcceptanceContext(
                            true,
                            statement.NormalizedSubjectKey,
                            candidateContext.HasNegation,
                            hardRuleConflict,
                            !string.IsNullOrWhiteSpace(candidateContext.Text)));
                foreach (string evidence in decision.Evidence)
                {
                    executionEvidence.Add(evidence);
                }

                if (!_semanticProcessingOptions.ShadowOnly)
                {
                    AddActiveResultEvidence(
                        statement.NormalizedSubjectKey,
                        ranking.ToSafeEvidence().Concat(decision.Evidence),
                        activeResultEvidence);
                }
            }
            catch (Exception exception)
            {
                executionEvidence.Add(string.Concat(
                    _semanticProcessingOptions.ShadowOnly
                        ? "SemanticShadow"
                        : "SemanticActive",
                    " | Status=Failed | DiagnosticCode=",
                    exception.GetType().Name,
                    " | FactPromotionAllowed=false"));
                if (!_semanticProcessingOptions.ShadowOnly)
                {
                    throw new InvalidOperationException(
                        "Local semantic runtime failed closed ("
                        + exception.GetType().Name
                        + ").");
                }
            }
        }

        private static SemanticCandidateRequest CreateRankingRequest(
            EvaluationMethodStatement statement,
            EvaluationMethodCandidateSafetyContext candidateContext)
        {
            string structuralLocation = string.Concat(
                statement.SourceLocation.DocumentPart,
                "|p=", statement.SourceLocation.ParagraphIndex,
                "|t=", statement.SourceLocation.TableIndex,
                "|r=", statement.SourceLocation.RowIndex,
                "|c=", statement.SourceLocation.CellIndex);
            if (candidateContext.StatementOffset != 0 || candidateContext.Text.Length != statement.OriginalText.Length)
            {
                // Offsets are relative to the extracted statement, not invented DOCX
                // character offsets. Distinct scopes must have distinct stable identities.
                structuralLocation += "|statement-scope=" + candidateContext.StatementOffset
                    + ":" + candidateContext.Text.Length;
            }
            string locationKey = "loc/v1:"
                + SemanticHash.ComputeSha256(structuralLocation);
            var location = new SemanticSourceLocation(
                statement.SourceDocument,
                locationKey,
                structuralLocation);
            var evidence = new SemanticCandidateEvidence(
                location,
                candidateContext.Text,
                "EvaluationMethodCandidateUnknown",
                SemanticHash.ComputeSha256(candidateContext.Text));
            var identity = new SemanticCandidateSetIdentity(
                SemanticCandidateType.EvaluationMethod,
                statement.NormalizedSubjectKey,
                locationKey,
                SemanticCandidateRole.AdoptedMethod);
            string requestId = "request/v1:"
                + SemanticHash.ComputeSha256(string.Concat(
                    statement.SourceDocument,
                    "|",
                    identity.CanonicalValue,
                    "|",
                    evidence.EvidenceHash));

            return new SemanticCandidateRequest(
                requestId,
                identity,
                new[] { evidence },
                new[] { "成本法", "市场法", "收益法", "重置成本法" },
                "evaluation-method/v1",
                "bge-top2-reranker/v1",
                true);
        }

        private static void AddActiveResultEvidence(
            string subjectKey,
            IEnumerable<string> evidence,
            IDictionary<string, List<string>> activeResultEvidence)
        {
            if (!activeResultEvidence.TryGetValue(subjectKey, out List<string> items))
            {
                items = new List<string>();
                activeResultEvidence.Add(subjectKey, items);
            }

            foreach (string item in evidence)
            {
                if (!items.Contains(item, StringComparer.Ordinal))
                {
                    items.Add(item);
                }
            }
        }

        private static IReadOnlyList<ConsistencyResult> AttachActiveSemanticEvidence(
            IReadOnlyList<ConsistencyResult> results,
            IReadOnlyDictionary<string, List<string>> activeResultEvidence)
        {
            return results.Select(result =>
            {
                if (!activeResultEvidence.TryGetValue(
                    result.Identity.SubjectKey,
                    out List<string> semanticEvidence)
                    || semanticEvidence.Count == 0)
                {
                    return result;
                }

                return new ConsistencyResult(
                    result.Identity,
                    result.Status,
                    result.Reason,
                    result.Evidence.Concat(semanticEvidence));
            }).ToArray();
        }

        private static IEnumerable<string> CreateFailureEvidence(
            V2DocumentAnalysisInput firstDocument,
            V2DocumentAnalysisInput secondDocument)
        {
            return new[]
            {
                firstDocument.SourceDocument + " | EvaluationMethod execution failed",
                secondDocument.SourceDocument + " | EvaluationMethod execution failed"
            };
        }
    }
}
