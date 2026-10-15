using System;
using System.Threading;

namespace AssetAppraisalChecker.Desktop.Services.Navigation
{
    /// <summary>Optional local WPS Writer COM navigation; never required for document checking.</summary>
    public sealed class WpsNavigationAdapter : ISourceEditorNavigationAdapter
    {
        private readonly OfficeComNavigationAdapter _adapter;
        public WpsNavigationAdapter(Func<Type> applicationTypeResolver = null)
        {
            _adapter = new OfficeComNavigationAdapter("KWPS.Application", "WPS", true, applicationTypeResolver);
        }
        public SourceNavigationResult Navigate(SourceNavigationTarget target, CancellationToken cancellationToken) =>
            _adapter.Navigate(target, cancellationToken);
    }

    public sealed class WpsPreferredNavigationAdapter : ISourceEditorNavigationAdapter
    {
        private readonly Func<bool> _wpsAvailable;
        private readonly ISourceEditorNavigationAdapter _wps;
        private readonly ISourceEditorNavigationAdapter _word;

        public WpsPreferredNavigationAdapter(Func<bool> wpsAvailable = null,
            ISourceEditorNavigationAdapter wps = null, ISourceEditorNavigationAdapter word = null)
        {
            _wpsAvailable = wpsAvailable ?? (() => Type.GetTypeFromProgID("KWPS.Application") != null);
            _wps = wps ?? new WpsNavigationAdapter();
            _word = word ?? new WordNavigationAdapter();
        }

        public SourceNavigationResult Navigate(SourceNavigationTarget target, CancellationToken cancellationToken)
        {
            // A registered but failing WPS must report failure, not silently switch editors.
            return (_wpsAvailable() ? _wps : _word).Navigate(target, cancellationToken);
        }
    }
}
