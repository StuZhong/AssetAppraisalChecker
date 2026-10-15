using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetAppraisalChecker.Desktop.Workflow;

namespace AssetAppraisalChecker.Desktop.Services.Navigation
{
    public sealed class SourceNavigationService : ISourceNavigationService
    {
        private readonly ISourceEditorNavigationAdapter _adapter;
        private readonly SourceLocationResolver _resolver;
        private readonly TimeSpan _timeout;
        private int _inFlight;

        public SourceNavigationService(ISourceEditorNavigationAdapter adapter = null, TimeSpan? timeout = null)
        {
            _adapter = adapter ?? new WpsPreferredNavigationAdapter();
            _resolver = new SourceLocationResolver();
            _timeout = timeout ?? TimeSpan.FromSeconds(20);
        }

        public bool IsNavigationPending => Volatile.Read(ref _inFlight) != 0;

        public static CheckedDocumentSource FindSource(IReadOnlyList<CheckedDocumentSource> sources, ReportCheckOccurrence occurrence)
        {
            if (sources == null || occurrence == null) return null;
            // Unlabelled V1 occurrences are unambiguous only in a single-document result.
            if (string.IsNullOrEmpty(occurrence.DocumentLabel)) return sources.Count == 1 ? sources[0] : null;
            var matches = sources.Where(s => s.Label == occurrence.DocumentLabel).ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        public async Task<SourceNavigationResult> NavigateAsync(CheckedDocumentSource source, ReportCheckOccurrence occurrence)
        {
            if (source == null || occurrence == null)
                return SourceNavigationResult.Unavailable("缺少可验证的文档来源或定位信息，无法定位。请重新校检。");
            if (!string.IsNullOrEmpty(occurrence.DocumentLabel) && occurrence.DocumentLabel != source.Label)
                return SourceNavigationResult.Unavailable("问题来源与所选文档不一致，未定位。");
            if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
                return SourceNavigationResult.Unavailable("编辑器上一次定位尚未返回，请处理 WPS/Word 中的提示后再试。");
            var completion = new TaskCompletionSource<SourceNavigationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellation = new CancellationTokenSource();
            var thread = new Thread(() =>
            {
                try
                {
                    if (!File.Exists(source.FullPath))
                        completion.TrySetResult(SourceNavigationResult.Unavailable("原文文件不存在，请重新选择并校检。"));
                    else if (!source.IsUnchanged())
                        completion.TrySetResult(SourceNavigationResult.Unavailable("原文已在校检后发生变化，请重新校检后定位。"));
                    else
                    {
                        SourceNavigationTarget target = _resolver.Resolve(source, occurrence);
                        var result = target == null
                            ? SourceNavigationResult.Unavailable("该位置无法可靠映射到编辑器；可打开原文并手动查找片段。")
                            : _adapter.Navigate(target, cancellation.Token);
                        if (!source.IsUnchanged()) result = SourceNavigationResult.Unavailable("定位期间原文发生变化，请重新校检。未保存或修改原文。");
                        completion.TrySetResult(result);
                    }
                }
                catch (Exception exception) when (!(exception is AccessViolationException))
                {
                    completion.TrySetResult(SourceNavigationResult.Unavailable("无法使用 WPS/Word 精确定位功能。文件可能不可读取、编辑器正忙或本机接口不支持；可打开原文并手动查找。"));
                }
                finally
                {
                    Interlocked.Exchange(ref _inFlight, 0);
                    cancellation.Dispose();
                }
            }) { IsBackground = true, Name = "Source navigation STA" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Task winner = await Task.WhenAny(completion.Task, Task.Delay(_timeout));
            if (winner == completion.Task) return await completion.Task;
            // Never abort or kill a user's Word process. The adapter checks cancellation before Select.
            try { cancellation.Cancel(); } catch (ObjectDisposedException) { }
            return SourceNavigationResult.Unavailable("编辑器定位未在限定时间内返回，请处理 WPS/Word 中的提示。未确认定位成功。");
        }
    }
}
