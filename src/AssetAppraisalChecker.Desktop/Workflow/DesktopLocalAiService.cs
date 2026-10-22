using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AssetAppraisalChecker.Application;
using AssetAppraisalChecker.Semantic;
using AssetAppraisalChecker.Semantic.LocalRuntime;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public interface ILocalAiStatusProvider
    {
        string AiStatusMessage { get; }
    }

    /// <summary>Desktop composition only: the existing pipeline owns safety and adjudication.</summary>
    public sealed class DesktopLocalAiService : IReportCheckService, ILocalAiStatusProvider, IDisposable
    {
        private readonly ObservedRanker _ranker;
        private readonly ReportCheckService _rules;
        private readonly string _unavailableStatus;
        private readonly SemaphoreSlim _checkLock = new SemaphoreSlim(1, 1);
        private string _status;
        private bool _disposed;

        public DesktopLocalAiService(Func<ISemanticCandidateRanker> runtimeFactory,
            string unavailableReason = null)
        {
            if (runtimeFactory == null)
            {
                _rules = new ReportCheckService();
                _status = unavailableReason ?? "AI 不可用：未配置本地模型路径；规则校检可用。";
                _unavailableStatus = _status;
            }
            else
            {
                _ranker = new ObservedRanker(runtimeFactory);
                _rules = new ReportCheckService(new V2DocumentAnalysisPipeline(
                    new FailClosedSemanticAnalyzer(), _ranker,
                    SemanticCandidateProcessingOptions.Active));
                _status = "离线 AI 已配置，尚未加载或执行；仅在存在受支持候选时调用。";
            }
        }

        public string AiStatusMessage => _status;

        public static DesktopLocalAiService CreateConfigured()
        {
            try
            {
                string root = ConfigurationManager.AppSettings["LocalModelRoot"];
                if (string.IsNullOrWhiteSpace(root))
                    root = Environment.GetEnvironmentVariable("ASSETAPPRAISALCHECKER_MODEL_ROOT");
                if (string.IsNullOrWhiteSpace(root)) return new DesktopLocalAiService(null);
                string absoluteRoot = ResolvePath(root);
                if (!Directory.Exists(absoluteRoot))
                    return new DesktopLocalAiService(null,
                        "AI 不可用：本地模型根目录不存在；规则校检可用。");
                string extensions = ConfigurationManager.AppSettings["OrtExtensionsLibraryPath"];
                string absoluteExtensions = ResolvePath(string.IsNullOrWhiteSpace(extensions)
                    ? "ortextensions.dll" : extensions);
                return new DesktopLocalAiService(() => CreateRuntime(absoluteRoot, absoluteExtensions));
            }
            catch (Exception exception)
            {
                // Do not expose local paths, exception messages or document evidence.
                return new DesktopLocalAiService(null,
                    "AI 不可用：配置读取失败（" + exception.GetType().Name + "）；规则校检可用。");
            }
        }

        public static string ResolvePath(string path)
        {
            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ISemanticCandidateRanker CreateRuntime(string root, string extensions)
        {
            LocalAiDependencyCheck.Verify(AppDomain.CurrentDomain.BaseDirectory, extensions);
            return CreateRuntimeCore(root, extensions);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ISemanticCandidateRanker CreateRuntimeCore(string root, string extensions)
        {
            return LocalSemanticRuntime.Create(
                LocalSemanticRuntimeOptions.CreateV2Production(root, extensions));
        }

        public Task<ReportCheckResult> CheckAsync(string filePath, IProgress<string> progress = null)
        {
            return CheckAsync(new ReportCheckRequest(filePath, null, null), progress);
        }

        public async Task<ReportCheckResult> CheckAsync(ReportCheckRequest request,
            IProgress<string> progress = null)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            await _checkLock.WaitAsync().ConfigureAwait(false);
            bool succeeded = false;
            try
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DesktopLocalAiService));
                if (request.Mode != ReportCheckMode.WordExcel) _ranker?.BeginCheck();
                ReportCheckResult result = await _rules.CheckAsync(request, progress).ConfigureAwait(false);
                succeeded = result.IsSuccess;
                return result;
            }
            finally
            {
                if (request.Mode == ReportCheckMode.WordExcel)
                    _status = "本次为 V3 Word–Excel 金额校检；仅使用确定性规则，未调用 AI。";
                else if (_ranker != null)
                    _status = _ranker.Summary(request.HasDocumentB, succeeded);
                else
                    _status = _unavailableStatus;
                _checkLock.Release();
            }
        }

        public void Dispose()
        {
            // A normal window close must not dispose sessions underneath a running check.
            _checkLock.Wait();
            try
            {
                if (_disposed) return;
                _disposed = true;
                _ranker?.Dispose();
            }
            finally { _checkLock.Release(); }
        }

        private sealed class ObservedRanker : ISemanticCandidateRanker, IDisposable
        {
            private readonly Func<ISemanticCandidateRanker> _factory;
            private ISemanticCandidateRanker _runtime;
            private string _failure;
            private int _completed;
            private int _abstained;

            public ObservedRanker(Func<ISemanticCandidateRanker> factory) { _factory = factory; }
            public void BeginCheck() { _completed = 0; _abstained = 0; }

            public SemanticCandidateRankingResult Rank(SemanticCandidateRequest request,
                SemanticCandidateSelectionContext context)
            {
                if (_failure != null) throw new InvalidOperationException("LocalAiUnavailable");
                try
                {
                    if (_runtime == null) _runtime = _factory()
                        ?? throw new InvalidOperationException("LocalAiFactoryReturnedNull");
                    SemanticCandidateRankingResult result = _runtime.Rank(request, context)
                        ?? throw new InvalidOperationException("LocalAiOutputMissing");
                    if (result.Rankings.Any(item => item.RerankerRank.HasValue)) _completed++;
                    if (result.Status == SemanticCandidateRankingStatus.Abstained) _abstained++;
                    return result;
                }
                catch (Exception exception)
                {
                    _failure = exception.GetType().Name;
                    // The existing checker records technical failure / WARNING, never a fabricated FAIL.
                    throw new InvalidOperationException("LocalAiExecutionFailed:" + _failure);
                }
            }

            public string Summary(bool dual, bool succeeded)
            {
                if (_failure != null)
                    return "AI 不可用：资产校验、加载或推理失败（" + _failure
                        + "）；已停止 AI，不猜测业务结论，规则校检继续。请检查本地部署并重启。";
                if (!dual) return "本次为单文档规则校检；未调用 AI。";
                if (_completed == 0)
                    return succeeded
                        ? "本次未执行 AI 推理：没有需要排序的受支持候选；规则校检正常运行。"
                        : "本次校检未完成；未确认 AI 推理，请查看文件读取或校检提示。";
                return "本次离线 AI 实际推理完成：BGE Top2 → Reranker，"
                    + _completed + " 次；安全弃权 " + _abstained
                    + " 次。仅提供排序证据，不自动晋升业务事实。";
            }

            public void Dispose() { (_runtime as IDisposable)?.Dispose(); }
        }
    }
}
