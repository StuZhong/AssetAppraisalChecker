using System.Threading;
using System.Threading.Tasks;
using AssetAppraisalChecker.Desktop.Workflow;

namespace AssetAppraisalChecker.Desktop.Services.Navigation
{
    public enum SourceNavigationStatus { Exact, ParagraphOnly, TableCell, TableRow, Unavailable }

    public sealed class SourceNavigationResult
    {
        public SourceNavigationResult(SourceNavigationStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }
        public SourceNavigationStatus Status { get; }
        public string Message { get; }
        public static SourceNavigationResult Unavailable(string message) =>
            new SourceNavigationResult(SourceNavigationStatus.Unavailable, message);
    }

    public interface ISourceNavigationService
    {
        Task<SourceNavigationResult> NavigateAsync(CheckedDocumentSource source, ReportCheckOccurrence occurrence);
    }

    public interface ISourceEditorNavigationAdapter
    {
        SourceNavigationResult Navigate(SourceNavigationTarget target, CancellationToken cancellationToken);
    }

    // Preserve the previous injectable Word contract for existing callers and tests.
    public interface IWordNavigationAdapter : ISourceEditorNavigationAdapter { }
}
