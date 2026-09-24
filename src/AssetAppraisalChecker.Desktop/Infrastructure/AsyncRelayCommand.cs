using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AssetAppraisalChecker.Desktop.Infrastructure
{
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute());
        }

        public async void Execute(object parameter)
        {
            try
            {
                await ExecuteAsync();
            }
            catch (Exception exception)
            {
                Trace.TraceError("Async command failed: {0}", exception);
            }
        }

        public async Task ExecuteAsync()
        {
            if (!CanExecute(null))
            {
                return;
            }

            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute();
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
